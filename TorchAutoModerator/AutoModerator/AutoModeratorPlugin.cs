using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Core;
using EntityGpsBroadcasters.Core;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Utils.General;
using Utils.Torch;

namespace AutoModerator
{
    public class AutoModeratorPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        Persistent<AutoModeratorConfig> _config;
        UserControl _userControl;
        CancellationTokenSource _canceller;
        FileLoggingConfigurator _fileLoggingConfigurator0;
        FileLoggingConfigurator _fileLoggingConfigurator1;

        GridReportTimeSeries _gridReportTimeSeries;
        GridReporter _gridReporter;
        LaggyGridScanner _laggyGridScanner;
        GridReportGpsFactory _gridReportGpsFactory;
        GridReportDescriber _gridReportDescriber;
        TargetPlayerCollector _targetPlayerCollector;
        EntityGpsBroadcaster _entityGpsBroadcaster;
        ServerLagObserver _serverLagObserver;

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

            _fileLoggingConfigurator0 = new FileLoggingConfigurator("AutoModerator", "AutoModerator.*", AutoModeratorConfig.DefaultLogFilePath);
            _fileLoggingConfigurator0.Initialize();

            _fileLoggingConfigurator1 = new FileLoggingConfigurator("EntityGpsBroadcasters", "EntityGpsBroadcasters.*", AutoModeratorConfig.DefaultLogFilePath);
            _fileLoggingConfigurator1.Initialize();

            _canceller = new CancellationTokenSource();

            var gpsHashFilePath = this.MakeFilePath("gpsHashes.txt");
            _entityGpsBroadcaster = new EntityGpsBroadcaster(gpsHashFilePath);

            _gridReportTimeSeries = new GridReportTimeSeries();
            _gridReporter = new GridReporter(Config);
            _laggyGridScanner = new LaggyGridScanner(Config, _gridReportTimeSeries);
            _gridReportDescriber = new GridReportDescriber(Config);
            _gridReportGpsFactory = new GridReportGpsFactory(_gridReportDescriber);
            _targetPlayerCollector = new TargetPlayerCollector(Config);
            _serverLagObserver = new ServerLagObserver(Config, 5);

            Config.PropertyChanged += (_, __) => OnConfigChanged();
            OnConfigChanged();
        }

        void OnGameLoaded()
        {
            _entityGpsBroadcaster.SendDeleteAllTrackedGpss();

            var canceller = _canceller.Token;
            TaskUtils.RunUntilCancelledAsync(MainLoop, canceller).Forget(Log);
            TaskUtils.RunUntilCancelledAsync(_serverLagObserver.LoopObserving, canceller).Forget(Log);
        }

        void OnGameUnloading()
        {
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
        }

        void OnConfigChanged()
        {
            _fileLoggingConfigurator0.Reconfigure(Config);
            _fileLoggingConfigurator1.Reconfigure(Config);
        }

        async Task MainLoop(CancellationToken canceller)
        {
            Log.Info("Started collector loop");

            // clear all GPS entities from the last session
            _entityGpsBroadcaster.SendDeleteAllTrackedGpss();

            // Wait for some time during the session startup
            await Task.Delay(Config.FirstIdleSeconds.Seconds(), canceller);

            while (!canceller.IsCancellationRequested)
            {
                try
                {
                    // profile grids
                    var gridReports = await _gridReporter.Profile(10.Seconds(), canceller);
                    _gridReportTimeSeries.AddReports(gridReports);

                    // drop old data
                    var thresholdTimestamp = DateTime.UtcNow - Config.BufferSeconds.Seconds(); // long enough
                    _gridReportTimeSeries.RemoveReportsOlderThan(thresholdTimestamp);

                    if (Config.EnableBroadcasting)
                    {
                        var laggyGridReports = _laggyGridScanner.ScanLaggyGrids();
                        await Broadcast(laggyGridReports);
                    }
                    else
                    {
                        _entityGpsBroadcaster.SendDeleteAllTrackedGpss();
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Error(e);

                    // wait a bit otherwise the logs will flood the UI
                    await Task.Delay(5.Seconds(), canceller);
                }
            }
        }

        public async Task<IEnumerable<GridReport>> ScanSpotLaggyGrids(TimeSpan profileTime)
        {
            var gridReports = await _gridReporter.Profile(profileTime, _canceller.Token);
            var laggyGridReports = gridReports.Where(r => r.ThresholdNormal >= 1);
            return laggyGridReports;
        }

        public async Task Broadcast(IEnumerable<GridReport> gridReports)
        {
            var gpss = await _gridReportGpsFactory.CreateGpss(gridReports, _canceller.Token);
            var playerIds = _targetPlayerCollector.GetTargetPlayerIds();
            _entityGpsBroadcaster.SendAdd(gpss, playerIds);
        }

        public void DeleteAllTrackedGpss()
        {
            _gridReportTimeSeries.Clear();
            _entityGpsBroadcaster.SendDeleteAllTrackedGpss();
        }

        public IEnumerable<MyGps> GetAllTrackedGpsEntities()
        {
            return _entityGpsBroadcaster.GetAllTrackedGpss();
        }
    }
}