using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Core;
using AutoModerator.Grids;
using AutoModerator.Players;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using TorchEntityGpsBroadcaster.Core;
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

        BroadcastListenerCollection _players;
        EntityIdGpsCollection _gpsCollection;
        GridLagPinCreator _gridLagPins;
        GridLagAnalyzer _gridLagAnalyzer;

        public AutoModeratorConfig Config => _config.Data;

        UserControl IWpfPlugin.GetControl() => _config.GetOrCreateUserControl(ref _userControl);

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

            _players = new BroadcastListenerCollection(Config);
            _gpsCollection = new EntityIdGpsCollection("<!> ");
            _gridLagPins = new GridLagPinCreator(Config);
            _gridLagAnalyzer = new GridLagAnalyzer(Config);
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

            // clear all GPS entities from the last session
            _gpsCollection.SendDeleteAllGpss();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdle.Seconds(), canceller);

            // MAIN LOOP
            while (!canceller.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();

                // auto profile
                var mask = new GameEntityMask(null, null, null);
                using (var gridProfiler = new GridProfiler(mask))
                using (var playerProfiler = new PlayerProfiler(mask))
                using (ProfilerResultQueue.Profile(gridProfiler))
                using (ProfilerResultQueue.Profile(playerProfiler))
                {
                    gridProfiler.MarkStart();
                    Log.Debug("auto-profiling...");

                    await Task.Delay(Config.ProfileTime.Seconds(), canceller);
                    Log.Debug("auto-profile done");

                    // grids
                    var gridProfileResult = gridProfiler.GetResult();
                    var gridLagProfileResults = _gridLagAnalyzer.Analyze(gridProfileResult).ToArray();
                    _gridLagPins.AddProfileInterval(gridLagProfileResults);
                    Log.Debug($"found {gridLagProfileResults.Count(r => r.ThresholdNormal > 1f)} laggy grids");
                    Log.Debug($"found {_gridLagPins.PinnedGridCount} pinned grids");

                    // todo give players a heads-up when their grids are laggy
                    // use `gridLagProfileResults`'s laggy grids (not the "pinned" grids which are already broadcasted)

                    // players
                }

                if (Config.EnableBroadcasting)
                {
                    var allGpsSources = new List<(IEntityGpsSource GpsSource, int Rank)>();

                    // collect from auto grid profiler results
                    if (Config.EnableGridBroadcasting)
                    {
                        var rankedSources = _gridLagPins
                            .CreateGpsSources(Config)
                            .OrderByDescending(g => g.LagNormal)
                            .Take(Config.MaxProfiledGridCount)
                            .Select((g, i) => ((IEntityGpsSource) g, i))
                            .ToArray();

                        allGpsSources.AddRange(rankedSources);
                        Log.Debug($"broadcasting {rankedSources.Length} laggy grids");
                    }

                    if (Config.EnablePlayerBroadcasting)
                    {
                        //todo
                    }

                    // MyGps can be created in the game loop only (idk why)
                    // this is inside the main loop & you better keep it performant
                    await GameLoopObserver.MoveToGameLoop(canceller);

                    var gpss = new List<MyGps>();
                    foreach (var (report, rank) in allGpsSources)
                    {
                        if (report.TryCreateGps(rank + 1, out var gps))
                        {
                            gpss.Add(gps);
                        }
                    }

                    await TaskUtils.MoveToThreadPool(canceller);

                    var targetIds = _players.GetReceiverIdentityIds();
                    _gpsCollection.SendReplaceAllTrackedGpss(gpss, targetIds);
                    Log.Debug($"broadcasted {allGpsSources.Count} laggy entities");
                }
                else
                {
                    _gpsCollection.SendDeleteAllGpss();
                    Log.Debug("deleted all tracked gpss");
                }

                await TaskUtils.DelayMax(stopwatch, 1.Seconds(), canceller);
            }
        }

        public bool CheckPlayerReceivesGpss(MyPlayer player)
        {
            return _players.CheckReceive(player);
        }

        public void DeleteAllGpss()
        {
            _gpsCollection.SendDeleteAllGpss();
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }
    }
}