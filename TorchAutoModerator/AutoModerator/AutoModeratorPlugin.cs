using System;
using System.Collections.Concurrent;
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
        SelfModQuestCollection _selfModQuests;
        ConcurrentDictionary<long, long> _playerIdToLaggiestGridIds;

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
            _selfModQuests = new SelfModQuestCollection();
            _playerIdToLaggiestGridIds = new ConcurrentDictionary<long, long>();
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
            Log.Info("started main");

            _entityGpsBroadcaster.ClearGpss();
            _selfModQuests.Clear();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdle.Seconds(), canceller);

            Log.Info("started collector loop");

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
                    gridProfiler.MarkStart();
                    playerProfiler.MarkStart();
                    await Task.Delay(Config.ProfileTime.Seconds(), canceller);
                    Log.Debug("auto-profile done");

                    // grids
                    var gridProfileResult = gridProfiler.GetResult();
                    var gridLagSnapshots = _gridLagSnapshotCreator.CreateLagSnapshots(gridProfileResult).ToArray();

                    // players
                    var playerProfileResult = playerProfiler.GetResult();
                    var playerLagSnapshots = _playerLagSnapshotCreator.CreateLagSnapshots(playerProfileResult).ToArray();

                    _laggyGrids.Update(gridLagSnapshots);
                    _laggyPlayers.Update(playerLagSnapshots);

                    // map player id -> their laggiest grid's id
                    var laggiestGridIds = gridLagSnapshots
                        .OrderByDescending(s => s.LagNormal)
                        .ToDictionaryDescending(s => s.OwnerId, s => s.EntityId);

                    var trackedPlayerIds = _laggyPlayers.GetTrackedEntityIds();
                    _playerIdToLaggiestGridIds.RemoveRangeExceptWith(trackedPlayerIds);
                    _playerIdToLaggiestGridIds.AddRange(laggiestGridIds);
                }

                Log.Debug("profile done");

                if (Config.EnableSelfModeration)
                {
                    var laggiestGrids = _laggyGrids.GetTrackedEntitySnapshots(Config.SelfModerationNormal);
                    var laggiestPlayers = _laggyPlayers.GetTrackedEntitySnapshots(Config.SelfModerationNormal);
                    var laggyPlayerSnapshotCreator = new LaggyPlayerSnapshotCreator();
                    var laggyPlayerSnapshots = await laggyPlayerSnapshotCreator.CreateSnapshots(laggiestPlayers, laggiestGrids, canceller);
                    _selfModQuests.Update(laggyPlayerSnapshots);
                    Log.Debug("quests done");
                }
                else
                {
                    _selfModQuests.Clear();
                    Log.Debug("cleared all quests");
                }

                if (Config.EnableBroadcasting)
                {
                    var allGpsSources = new Dictionary<long, IEntityGpsSource>();

                    if (Config.EnablePlayerBroadcasting)
                    {
                        foreach (var (snapshot, rank) in _laggyPlayers.GetTopPins().Indexed())
                        {
                            if (_playerIdToLaggiestGridIds.TryGetValue(snapshot.EntityId, out var gridId))
                            {
                                var gpsSource = new PlayerGpsSource(Config, snapshot, gridId, rank);
                                allGpsSources[gpsSource.GridId] = gpsSource;
                            }
                            else
                            {
                                var player = MySession.Static.Players.TryGetPlayerById(snapshot.EntityId, out var p) ? p : null;
                                Log.Warn($"laggy grid not found for laggy player: {player?.DisplayName ?? "<no name>"} ({snapshot.EntityId})");
                            }
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

                    Log.Debug("broadcast done");
                }
                else
                {
                    // you could also do this right at the moment the config's changed
                    _entityGpsBroadcaster.ClearGpss();
                    Log.Debug("deleted all tracked gpss");
                }

                Log.Debug("interval done");
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

        public void OnSelfProfiled(long playerId)
        {
            _selfModQuests.OnSelfProfiled(playerId);
        }
    }
}