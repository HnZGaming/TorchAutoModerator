using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Core;
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

        EntityLagTimeSeries _lagTimeSeries;
        Dictionary<long, GridLagProfileResult> _referenceProfileResults;
        LifespanCollection<long> _autoBroadcastableGridIds;
        LifespanCollection<long> _manualBroadcastableGridIds;
        BroadcastListenerCollection _players;
        EntityIdGpsCollection _gpsCollection;
        ServerLagObserver _lagObserver;

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
                new[] {"AutoModerator.*", "Utils.EntityGps.*"},
                AutoModeratorConfig.DefaultLogFilePath);

            _fileLoggingConfigurator.Initialize();
            _fileLoggingConfigurator.Configure(Config);

            _canceller = new CancellationTokenSource();

            _lagTimeSeries = new EntityLagTimeSeries(Config);
            _referenceProfileResults = new Dictionary<long, GridLagProfileResult>();
            _players = new BroadcastListenerCollection(Config);
            _gpsCollection = new EntityIdGpsCollection("! ");
            _lagObserver = new ServerLagObserver(5.Seconds());
            _manualBroadcastableGridIds = new LifespanCollection<long>();
            _autoBroadcastableGridIds = new LifespanCollection<long>();
        }

        void OnGameLoaded()
        {
            TaskUtils.RunUntilCancelledAsync(Main, _canceller.Token).Forget(Log);
            TaskUtils.RunUntilCancelledAsync(_lagObserver.Observe, _canceller.Token).Forget(Log);
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
        }

        async Task Main(CancellationToken canceller)
        {
            Log.Info("started collector loop");

            // clear all GPS entities from the last session
            _gpsCollection.SendDeleteUntrackedGpss();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdle.Seconds(), canceller);

            while (!canceller.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();

                // auto profile
                var mask = new GameEntityMask(null, null, null);
                using (var profiler = new GridLagProfiler(Config, mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    profiler.MarkStart();
                    Log.Debug("auto-profiling...");

                    var profilingTime = Config.ProfileTime.Seconds();
                    await Task.Delay(profilingTime, canceller);

                    var profileResults = profiler.GetProfileResults(50).ToArray();
                    _lagTimeSeries.Update(profileResults.Select(r => (r.GridId, r.ThresholdNormal)));
                    Log.Debug($"auto-profiled {profileResults.Length} grids");

                    // map grid id -> last profile result
                    _referenceProfileResults.AddRange(profileResults.Select(r => (r.GridId, r)));

                    // expire old data
                    var trackedGridIds = CollectionUtils.Merge(
                        _lagTimeSeries.GridIds,
                        _autoBroadcastableGridIds.Keys,
                        _manualBroadcastableGridIds.Keys);
                    _referenceProfileResults.RemoveRangeExceptWith(trackedGridIds);

                    // keep track of laggy grids & gps lifespan
                    var laggyGridIds = _lagTimeSeries.GetLaggyGridIds().ToArray();
                    _autoBroadcastableGridIds.AddOrUpdate(laggyGridIds);
                    Log.Debug($"found {laggyGridIds.Length} laggy grids");
                }

                // check if the server is laggy
                var simSpeed = _lagObserver.SimSpeed;
                var isLaggy = simSpeed < Config.SimSpeedThreshold;
                Log.Debug($"laggy: {isLaggy} ({simSpeed:0.0}ss)");

                // GPSs should disappear in a set length of time
                var gpsLifespan = Config.GpsLifespan.Seconds();
                _autoBroadcastableGridIds.RemoveExpired(gpsLifespan);
                _manualBroadcastableGridIds.RemoveExpired(gpsLifespan);

                if (Config.EnableBroadcasting && isLaggy)
                {
                    var allReports = new Dictionary<long, GridLagReport>();

                    // collect from auto profiler results
                    if (Config.EnableAutoBroadcasting)
                    {
                        var ids = _autoBroadcastableGridIds.GetRemainingTimes().ToArray();
                        var reports = ids.Select(p => new GridLagReport(Config, _referenceProfileResults[p.Key], p.RemainingTime));
                        allReports.AddRange(reports.Select(r => (r.GridId, r)));
                        Log.Debug($"auto-broadcasting {ids.Length} grids");
                    }

                    // collect from manual profiler results (via admin commands)
                    {
                        var ids = _manualBroadcastableGridIds.GetRemainingTimes().ToArray();
                        var reports = ids.Select(p => new GridLagReport(Config, _referenceProfileResults[p.Key], p.RemainingTime));
                        allReports.AddRange(reports.Select(r => (r.GridId, r)));
                        Log.Debug($"manual-broadcasting {ids.Length} grids");
                    }

                    // MyGps can be created in the game loop only (idk why)
                    await GameLoopObserver.MoveToGameLoop(canceller);

                    // create GPS entities of laggy grids
                    var gpss = new List<MyGps>();
                    foreach (var (report, i) in allReports.Values.Select((r, i) => (r, i)))
                    {
                        if (report.TryCreateGps(i + 1, out var gps))
                        {
                            gpss.Add(gps);
                        }
                    }

                    await TaskUtils.MoveToThreadPool(canceller);

                    var targetIds = _players.GetReceiverIdentityIds();
                    _gpsCollection.SendReplaceAllTrackedGpss(gpss, targetIds);
                    Log.Debug($"sent gps of {allReports.Count} laggy grids");
                }
                else
                {
                    _gpsCollection.SendDeleteAllTrackedGpss();
                    Log.Debug("deleted all tracked gpss");
                }

                await TaskUtils.DelayMax(stopwatch, 1.Seconds(), canceller);
            }
        }

        public void BroadcastGpss(IEnumerable<GridLagProfileResult> profileResults)
        {
            // the next main loop will broadcast GPSs
            _manualBroadcastableGridIds.AddOrUpdate(profileResults.Select(r => r.GridId));
            _referenceProfileResults.AddRange(profileResults.Select(r => (r.GridId, r)));
        }

        public bool CheckPlayerReceivesGpss(MyPlayer player)
        {
            return _players.CheckReceive(player);
        }

        public void DeleteAllGpss()
        {
            _gpsCollection.SendDeleteAllTrackedGpss();
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }
    }
}