using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Broadcasts;
using AutoModerator.Core;
using AutoModerator.Grids;
using AutoModerator.Players;
using AutoModerator.Quests;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Utils.General;
using Utils.Torch;

namespace AutoModerator
{
    public sealed class AutoModeratorPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        Persistent<AutoModeratorConfig> _config;
        UserControl _userControl;
        CancellationTokenSource _canceller;
        FileLoggingConfigurator _fileLoggingConfigurator;

        LaggyEntityTracker _laggyGrids;
        LaggyEntityTracker _laggyPlayers;
        GridLagSnapshotCreator _gridLagSnapshotCreator;
        PlayerLagSnapshotCreator _playerLagSnapshotCreator;
        EntityGpsBroadcaster _entityGpsBroadcaster;
        BroadcastListenerCollection _gpsReceivers;
        SelfModerationQuestCollection _selfModerationQuests;
        Dictionary<long, long> _playerLaggiestGrids;

        UserControl IWpfPlugin.GetControl() => _config.GetOrCreateUserControl(ref _userControl);
        public AutoModeratorConfig Config => _config.Data;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.ListenOnGameLoaded(OnGameLoaded);
            this.ListenOnGameUnloading(OnGameUnloading);

            GameLoopObserverManager.Add(torch);

            var configFilePath = this.MakeConfigFilePath();
            _config = Persistent<AutoModeratorConfig>.Load(configFilePath);
            Config.PropertyChanged += OnConfigChanged;

            _fileLoggingConfigurator = new FileLoggingConfigurator(
                "AutoModerator",
                new[] {"AutoModerator.*", "Utils.EntityGps.*", "Utils.TimeSerieses.*"},
                AutoModeratorConfig.DefaultLogFilePath);

            _fileLoggingConfigurator.Initialize();
            _fileLoggingConfigurator.Configure(Config);

            _canceller = new CancellationTokenSource();
            _laggyGrids = new LaggyEntityTracker(new LaggyGridTrackerConfig(Config));
            _laggyPlayers = new LaggyEntityTracker(new LaggyPlayerTrackerConfig(Config));
            _gridLagSnapshotCreator = new GridLagSnapshotCreator(Config);
            _playerLagSnapshotCreator = new PlayerLagSnapshotCreator(Config);
            _gpsReceivers = new BroadcastListenerCollection(Config);
            _entityGpsBroadcaster = new EntityGpsBroadcaster();
            _selfModerationQuests = new SelfModerationQuestCollection();
            _playerLaggiestGrids = new Dictionary<long, long>();
        }

        void OnGameLoaded()
        {
            TaskUtils.RunUntilCancelledAsync(Main, _canceller.Token).Forget(Log);
        }

        void OnGameUnloading()
        {
            Config.PropertyChanged -= OnConfigChanged;
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
        }

        void OnConfigChanged(object _, PropertyChangedEventArgs args)
        {
            _fileLoggingConfigurator.Configure(Config);
            Log.Info("config changed");
        }

        async Task Main(CancellationToken canceller)
        {
            Log.Info("started collector loop");

            _entityGpsBroadcaster.ClearGpss();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdle.Seconds(), canceller);

            // MAIN LOOP
            while (!canceller.IsCancellationRequested)
            {
                // auto profile
                var mask = new GameEntityMask(null, null, null);
                using (var gridProfiler = new GridProfiler(mask))
                using (var playerProfiler = new PlayerProfiler(mask))
                using (ProfilerResultQueue.Profile(gridProfiler))
                using (ProfilerResultQueue.Profile(playerProfiler))
                {
                    Log.Debug("auto-profiling...");
                    gridProfiler.MarkStart();
                    playerProfiler.MarkStart();
                    await Task.Delay(Config.ProfileTime.Seconds(), canceller);
                    Log.Debug("auto-profile done");

                    // grids
                    var gridProfileResult = gridProfiler.GetResult();
                    var gridLagSnapshots = _gridLagSnapshotCreator.CreateLagSnapshots(gridProfileResult).ToArray();
                    _laggyGrids.Update(gridLagSnapshots);

                    // players
                    var playerProfileResult = playerProfiler.GetResult();
                    var playerLagSnapshots = _playerLagSnapshotCreator.CreateLagSnapshots(playerProfileResult);
                    _laggyPlayers.Update(playerLagSnapshots);

                    // map player id -> their laggiest grid's id
                    var laggiestGridIds = gridLagSnapshots
                        .OrderBy(s => s.LagNormal)
                        .ToDictionary(s => s.OwnerId, s => s.EntityId);

                    var trackedPlayerIds = _laggyPlayers.GetTrackedEntityIds();
                    _playerLaggiestGrids.RemoveRangeExceptWith(trackedPlayerIds);
                    _playerLaggiestGrids.AddRange(laggiestGridIds);
                }

                if (Config.EnableSelfModeration)
                {
                    var lagSnapshots = _laggyPlayers
                        .GetTrackedEntitySnapshots(Config.SelfModerationMspf)
                        .ToDictionary(k => k.EntityId);

                    var playerStates = new List<(long, double, bool)>();
                    foreach (var (playerId, lagNormal) in lags)
                    {
                        var pinned = pins.TryGetValue(playerId, out var p) && p;
                        playerStates.Add((playerId, lagNormal, pinned));
                    }

                    _selfModerationQuests.Update(playerStates, canceller);
                }
                else
                {
                    _selfModerationQuests.Clear();
                }

                if (Config.EnableBroadcasting)
                {
                    var allGpsSources = new Dictionary<long, IEntityGpsSource>();

                    if (Config.EnablePlayerBroadcasting)
                    {
                        foreach (var (snapshot, rank) in _laggyPlayers.GetTopPins().Indexed())
                        {
                            var gridId = _playerLaggiestGrids[snapshot.EntityId];
                            var gpsSource = new PlayerGpsSource(Config, snapshot, gridId, rank);
                            allGpsSources[gpsSource.GridId] = gpsSource;
                        }
                    }

                    if (Config.EnableGridBroadcasting)
                    {
                        foreach (var (snapshot, rank) in _laggyGrids.GetTopPins().Indexed())
                        {
                            var gpsSource = new GridGpsSource(Config, snapshot, rank);
                            allGpsSources[gpsSource.GridId] = gpsSource;
                        }
                    }

                    await _entityGpsBroadcaster.ReplaceGpss(
                        allGpsSources.Values,
                        _gpsReceivers.GetReceiverIdentityIds(),
                        canceller);
                }
                else
                {
                    // you could also do this right at the moment the config's changed
                    _entityGpsBroadcaster.ClearGpss();
                    Log.Debug("deleted all tracked gpss");
                }
            }
        }

        public bool CheckPlayerReceivesGpss(MyPlayer player)
        {
            return _gpsReceivers.CheckReceive(player);
        }

        public void DeleteAllGpss()
        {
            _entityGpsBroadcaster.ClearGpss();
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _entityGpsBroadcaster.GetGpss();
        }
    }
}