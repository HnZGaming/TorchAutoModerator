using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Core;
using AutoModerator.Grids;
using AutoModerator.Players;
using AutoModerator.Punishes;
using AutoModerator.Punishes.Broadcasts;
using AutoModerator.Warnings;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Utils.General;
using Utils.TimeSerieses;
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
        LagPunishExecutor _punishExecutor;
        LagPunishChatFeed _punishChatFeed;

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
            _entityGpsBroadcaster = new EntityGpsBroadcaster(Config);
            _warningQuests = new LagWarningCollection(Config);
            _punishExecutor = new LagPunishExecutor(Config);
        }

        void OnGameLoaded()
        {
            var chatManager = Torch.CurrentSession.Managers.GetManager<IChatManagerServer>();
            chatManager.ThrowIfNull("chat manager not found");
            _punishChatFeed = new LagPunishChatFeed(Config, chatManager);

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
                    Log.Trace("auto-profile started");
                    gridProfiler.MarkStart();
                    playerProfiler.MarkStart();
                    await Task.Delay(Config.IntervalFrequency.Seconds(), canceller);
                    Log.Trace("auto-profile done");

                    _laggyGrids.Update(gridProfiler.GetResult());
                    _laggyPlayers.Update(playerProfiler.GetResult());
                }

                Log.Trace("profile done");

                if (Config.EnableWarning)
                {
                    var usePins = Config.PunishType != LagPunishType.None;
                    Log.Debug($"punishment type: {Config.PunishType}, warning for punishment: {usePins}");

                    var sources = new List<LagWarningSource>();
                    var players = _laggyPlayers.GetTrackedEntities(Config.WarningLagNormal).ToDictionary(p => p.Id);
                    var grids = _laggyGrids.GetPlayerLaggiestGrids(Config.WarningLagNormal).ToDictionary();
                    foreach (var (playerId, (player, grid)) in players.Zip(grids))
                    {
                        if (playerId == 0) continue; // grid not owned

                        var src = new LagWarningSource(
                            playerId,
                            MySession.Static.Players.GetPlayerNameOrElse(playerId, $"<{playerId}>"),
                            player.LongLagNormal,
                            usePins ? player.RemainingTime : TimeSpan.Zero,
                            grid.LongLagNormal,
                            usePins ? grid.RemainingTime : TimeSpan.Zero);

                        sources.Add(src);
                    }

                    _warningQuests.Update(sources);
                }
                else
                {
                    _warningQuests.Clear();
                }

                Log.Trace("warnings done");

                if (Config.EnablePunishChatFeed)
                {
                    var sources = new List<LagPunishChatSource>();
                    var grids = _laggyGrids.GetPlayerPinnedGrids().ToDictionary();
                    var players = _laggyPlayers.GetPinnedPlayers().ToDictionary(p => p.Id);
                    foreach (var (playerId, (laggiestGrid, player)) in grids.Zip(players))
                    {
                        var lagNormal = Math.Max(laggiestGrid.LongLagNormal, player.LongLagNormal);
                        var isPinned = laggiestGrid.IsPinned || player.IsPinned;
                        var source = new LagPunishChatSource(playerId, laggiestGrid.Id, lagNormal, isPinned);
                        sources.Add(source);
                    }

                    await _punishChatFeed.Update(sources);
                }
                else
                {
                    _punishChatFeed.Clear();
                }

                if (Config.PunishType == LagPunishType.Damage ||
                    Config.PunishType == LagPunishType.Shutdown)
                {
                    var punishSources = new Dictionary<long, LagPunishSource>();
                    foreach (var pinnedPlayer in _laggyPlayers.GetPinnedPlayers())
                    {
                        var playerId = pinnedPlayer.Id;
                        if (!_laggyGrids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid))
                        {
                            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"<{playerId}>");
                            Log.Warn($"laggy grid deleted by {playerName}");
                            continue;
                        }

                        var src = new LagPunishSource(laggiestGrid.Id, laggiestGrid.IsPinned);
                        punishSources[src.GridId] = src;
                    }

                    foreach (var grid in _laggyGrids.GetPinnedGrids())
                    {
                        var gpsSource = new LagPunishSource(grid.Id, grid.IsPinned);
                        punishSources[gpsSource.GridId] = gpsSource;
                    }

                    await _punishExecutor.Update(punishSources);
                }
                else
                {
                    _punishExecutor.Clear();
                }

                Log.Trace("punishment done");

                if (Config.PunishType == LagPunishType.Broadcast)
                {
                    var allGpsSources = new Dictionary<long, GridGpsSource>();

                    foreach (var (player, rank) in _laggyPlayers.GetPinnedPlayers().Indexed())
                    {
                        var playerId = player.Id;
                        if (!_laggyGrids.TryGetLaggiestGridOwnedBy(playerId, out var laggiestGrid))
                        {
                            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"<{playerId}>");
                            Log.Warn($"laggy grid deleted by {playerName}");
                            continue;
                        }

                        var gpsSource = new GridGpsSource(laggiestGrid.Id, player.LongLagNormal, player.RemainingTime, rank);
                        allGpsSources[gpsSource.GridId] = gpsSource;
                    }

                    foreach (var (grid, rank) in _laggyGrids.GetPinnedGrids().Indexed())
                    {
                        var gpsSource = new GridGpsSource(grid.Id, grid.LongLagNormal, grid.RemainingTime, rank);
                        allGpsSources[gpsSource.GridId] = gpsSource;
                    }

                    var targetIdentityIds = _gpsReceivers.GetReceiverIdentityIds();
                    await _entityGpsBroadcaster.ReplaceGpss(allGpsSources.Values, targetIdentityIds, canceller);
                }
                else
                {
                    _entityGpsBroadcaster.ClearGpss();
                }

                Log.Trace("broadcast done");
                Log.Debug("interval done");
            }
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

        public bool TryGetTimeSeries(long entityId, out ITimeSeries<double> timeSeries)
        {
            return _laggyGrids.TryGetTimeSeries(entityId, out timeSeries) ||
                   _laggyPlayers.TryGetTimeSeries(entityId, out timeSeries);
        }

        public IEnumerable<TrackedEntitySnapshot> GetTrackedGrids()
        {
            return _laggyGrids.GetTrackedEntities();
        }

        public IEnumerable<TrackedEntitySnapshot> GetTrackedPlayers()
        {
            return _laggyPlayers.GetTrackedEntities();
        }
    }
}