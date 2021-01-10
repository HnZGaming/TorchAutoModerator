using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Core;
using EntityGpsBroadcasters.Core;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Screens.Helpers;
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
        FileLoggingConfigurator _fileLoggingConfigurator0;
        FileLoggingConfigurator _fileLoggingConfigurator1;

        GridLagProfileTimeSeries _lagTimeSeries;
        GridLagReportGpsFactory _gpsFactory;
        GridLagReportDescriber _gpsDescriber;
        BroadcastReceiverCollector _gpsReceivers;
        EntityGpsBroadcaster _gpsBroadcaster;
        ServerLagObserver _lagObserver;
        ConcurrentDictionary<long, GridLagReport> _lastAutoGrids;
        GridLagProfileTimeline _manualGrids;

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
            Config.PropertyChanged += (_, arg) => OnConfigChanged(arg.PropertyName).Forget(Log);

            _fileLoggingConfigurator0 = new FileLoggingConfigurator("AutoModerator", "AutoModerator.*", AutoModeratorConfig.DefaultLogFilePath);
            _fileLoggingConfigurator0.Initialize();
            _fileLoggingConfigurator0.Reconfigure(Config);

            _fileLoggingConfigurator1 = new FileLoggingConfigurator("EntityGpsBroadcasters", "EntityGpsBroadcasters.*", AutoModeratorConfig.DefaultLogFilePath);
            _fileLoggingConfigurator1.Initialize();
            _fileLoggingConfigurator1.Reconfigure(Config);

            _canceller = new CancellationTokenSource();

            var gpsHashFilePath = this.MakeFilePath("gpsHashes.txt");
            _gpsBroadcaster = new EntityGpsBroadcaster(gpsHashFilePath);
            _lagTimeSeries = new GridLagProfileTimeSeries(Config);
            _gpsDescriber = new GridLagReportDescriber(Config);
            _gpsFactory = new GridLagReportGpsFactory(_gpsDescriber);
            _gpsReceivers = new BroadcastReceiverCollector(Config);
            _lagObserver = new ServerLagObserver(Config, 5.Seconds());
            _lastAutoGrids = new ConcurrentDictionary<long, GridLagReport>();
            _manualGrids = new GridLagProfileTimeline();
        }

        void OnGameLoaded()
        {
            _gpsBroadcaster.SendDeleteAllTrackedGpss();

            var canceller = _canceller.Token;
            TaskUtils.RunUntilCancelledAsync(MainLoop, canceller).Forget(Log);
            TaskUtils.RunUntilCancelledAsync(_lagObserver.Observe, canceller).Forget(Log);
        }

        void OnGameUnloading()
        {
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
        }

        async Task OnConfigChanged(string propertyName)
        {
            _fileLoggingConfigurator0.Reconfigure(Config);
            _fileLoggingConfigurator1.Reconfigure(Config);

            await TaskUtils.MoveToThreadPool();

            if (propertyName == nameof(Config.EnableAutoBroadcasting) &&
                !Config.EnableBroadcasting)
            {
                _gpsBroadcaster.SendDeleteAllTrackedGpss();
            }

            if (propertyName == nameof(Config.EnableAutoBroadcasting) &&
                !Config.EnableAutoBroadcasting)
            {
                // delete auto-broadcasted GPSs
                _gpsBroadcaster.SendDelete(_lastAutoGrids.Keys);
                _lastAutoGrids.Clear();
            }

            if (propertyName == nameof(Config.AdminsOnly) ||
                propertyName == nameof(Config.MutedPlayerIds))
            {
                _manualGrids.RemoveExpired(DateTime.UtcNow);

                // collect the present GPSs
                var presentReports = new Dictionary<long, GridLagReport>();
                presentReports.AddRange(_lastAutoGrids);
                presentReports.AddRange(_manualGrids.MakeReports(DateTime.UtcNow).ToDictionary(r => r.GridId));

                // delete all present GPSs
                _gpsBroadcaster.SendDelete(presentReports.Keys);

                // broadcast the same GPSs again with an updated set of receivers
                var gpss = await _gpsFactory.CreateGpss(presentReports.Values, _canceller.Token);
                var receiverIds = _gpsReceivers.GetReceiverIds();
                _gpsBroadcaster.SendAddOrModify(gpss, receiverIds);
            }
        }

        async Task MainLoop(CancellationToken canceller)
        {
            Log.Info("Started collector loop");

            // clear all GPS entities from the last session
            _gpsBroadcaster.SendDeleteAllTrackedGpss();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdle.Seconds(), canceller);

            while (!canceller.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();

                // profile grids & append into the time series
                var mask = new GameEntityMask(null, null, null);
                using (var profiler = new GridLagProfiler(Config, mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    profiler.MarkStart();

                    var profilingTime = Config.SampleFrequency.Seconds();
                    await Task.Delay(profilingTime, canceller);

                    var newProfiledGrids = profiler.GetProfileResults(50);
                    _lagTimeSeries.AddProfileResults(DateTime.UtcNow, newProfiledGrids);
                    _lagTimeSeries.RemoveOldResults(DateTime.UtcNow);
                }

                // delete auto GPSs from the last loop
                _gpsBroadcaster.SendDelete(_lastAutoGrids.Keys);
                _lastAutoGrids.Clear();

                // delete manual GPSs from the last loop
                _gpsBroadcaster.SendDelete(_manualGrids.GridIds);
                _manualGrids.RemoveExpired(DateTime.UtcNow);

                if (Config.EnableBroadcasting)
                {
                    if (Config.EnableAutoBroadcasting)
                    {
                        // find grids that should be broadcasted in this interval
                        var broadcastableGrids = _lagTimeSeries.GetCurrentBroadcastableGrids();
                        _lastAutoGrids.AddRange(broadcastableGrids.ToDictionary(r => r.GridId));

                        // broadcast
                        var gpss = await _gpsFactory.CreateGpss(broadcastableGrids, canceller);
                        var receiverIds = _gpsReceivers.GetReceiverIds();
                        _gpsBroadcaster.SendAddOrModify(gpss, receiverIds);
                    }

                    // manual broadcasting, overwrites auto if any
                    {
                        var gridReports = _manualGrids.MakeReports(DateTime.UtcNow);
                        var gpss = await _gpsFactory.CreateGpss(gridReports, canceller);
                        var receiverIds = _gpsReceivers.GetReceiverIds();
                        _gpsBroadcaster.SendAddOrModify(gpss, receiverIds);
                    }
                }

                await TaskUtils.SpendAtLeast(stopwatch, 1.Seconds(), canceller);
            }
        }

        public CancellationToken GetCancellationToken()
        {
            return _canceller.Token;
        }

        public GridLagProfiler GetProfiler(GameEntityMask mask)
        {
            return new GridLagProfiler(Config, mask);
        }

        public async Task Broadcast(IEnumerable<GridLagProfileResult> profileResults, TimeSpan remainingTime)
        {
            // remember the list so we can schedule countdown
            _manualGrids.AddProfileResults(profileResults, remainingTime);

            // broadcast
            var gridReports = profileResults.Select(r => new GridLagReport(r, remainingTime));
            var gpss = await _gpsFactory.CreateGpss(gridReports, _canceller.Token);
            var receiverIds = _gpsReceivers.GetReceiverIds();
            _gpsBroadcaster.SendAddOrModify(gpss, receiverIds);
        }

        public void DeleteAllTrackedGpss()
        {
            _gpsBroadcaster.SendDeleteAllTrackedGpss();
        }

        public IEnumerable<MyGps> GetAllTrackedGpsEntities()
        {
            return _gpsBroadcaster.GetAllTrackedGpss();
        }
    }
}