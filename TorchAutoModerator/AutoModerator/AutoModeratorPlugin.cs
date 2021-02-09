using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Broadcasts;
using AutoModerator.Grids;
using AutoModerator.Players;
using AutoModerator.Warnings;
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

        GridLagTracker _laggyGrids;
        PlayerLagTracker _laggyPlayers;
        EntityGpsBroadcaster _entityGpsBroadcaster;
        BroadcastListenerCollection _gpsReceivers;
        LagWarningCollection _warningQuests;

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
            _laggyGrids = new GridLagTracker(Config);
            _laggyPlayers = new PlayerLagTracker(Config);
            _gpsReceivers = new BroadcastListenerCollection(Config);
            _entityGpsBroadcaster = new EntityGpsBroadcaster();
            _warningQuests = new LagWarningCollection(Config);
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
            _entityGpsBroadcaster.ClearGpss();
            _warningQuests.Clear();
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
            _warningQuests.Clear();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdleTime.Seconds(), canceller);

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
                    Log.Debug("auto-profile started");
                    gridProfiler.MarkStart();
                    playerProfiler.MarkStart();
                    await Task.Delay(Config.ProfileFrequency.Seconds(), canceller);
                    Log.Debug("auto-profile done");

                    _laggyGrids.Update(gridProfiler.GetResult());
                    _laggyPlayers.Update(playerProfiler.GetResult());
                }

                Log.Debug("profile done");

                if (Config.EnableSelfModeration)
                {
                    var laggyPlayers = _laggyPlayers.GetTrackedEntities(Config.SelfModerationNormal);
                    var laggyPlayerReports = new List<LaggyPlayerSnapshot>();
                    foreach (var player in laggyPlayers)
                    {
                        var playerId = player.Id;
                        var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"<{playerId}>");

                        if (!_laggyGrids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid))
                        {
                            Log.Warn($"laggy grid not found for laggy player: {playerName}");
                        }

                        var laggyPlayerReport = new LaggyPlayerSnapshot(
                            playerId, playerName,
                            player.LongLagNormal / Config.SelfModerationNormal,
                            player.RemainingTime > TimeSpan.Zero,
                            laggiestGrid.LongLagNormal / Config.SelfModerationNormal,
                            laggiestGrid.RemainingTime > TimeSpan.Zero);

                        laggyPlayerReports.Add(laggyPlayerReport);
                    }

                    _warningQuests.Update(laggyPlayerReports);
                }
                else
                {
                    _warningQuests.Update(Enumerable.Empty<LaggyPlayerSnapshot>());
                }

                Log.Debug("warnings done");

                var allGpsSources = new Dictionary<long, IEntityGpsSource>();

                if (Config.EnablePlayerBroadcasting)
                {
                    foreach (var (player, rank) in _laggyPlayers.GetTopPins())
                    {
                        var playerId = player.Id;
                        if (!_laggyGrids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid))
                        {
                            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"<{playerId}>");
                            Log.Warn($"laggy grid not found for laggy player: {playerName}");
                            continue;
                        }

                        var gpsSource = new PlayerGpsSource(Config, player, laggiestGrid.Id, rank);
                        allGpsSources[gpsSource.GridId] = gpsSource;
                    }
                }

                if (Config.EnableGridBroadcasting)
                {
                    foreach (var (grid, rank) in _laggyGrids.GetTopPins())
                    {
                        var gpsSource = new GridGpsSource(Config, grid, rank);
                        allGpsSources[gpsSource.GridId] = gpsSource;
                    }
                }

                var targetIdentityIds = _gpsReceivers.GetReceiverIdentityIds();
                await _entityGpsBroadcaster.ReplaceGpss(allGpsSources.Values, targetIdentityIds, canceller);

                Log.Debug("broadcast done");
                Log.Debug("interval done");
            }
        }

        public bool CheckPlayerReceivesGpss(MyPlayer player)
        {
            return _gpsReceivers.CheckReceive(player);
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _entityGpsBroadcaster.GetGpss();
        }

        public void OnSelfProfiled(long playerId)
        {
            _warningQuests.OnSelfProfiled(playerId);
        }

        public void ClearCache()
        {
            _laggyGrids.Clear();
            _laggyPlayers.Clear();
        }

        public void ClearQuestForUser(long playerId)
        {
            _warningQuests.Clear(playerId);
        }
    }
}