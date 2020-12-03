using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using TorchShittyShitShitter.Core;
using Utils.General;
using Utils.Torch;

namespace TorchShittyShitShitter
{
    public class ShittyShitShitterPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        Persistent<ShittyShitShitterConfig> _config;
        UserControl _userControl;
        CancellationTokenSource _canceller;
        GpsBroadcaster _gpsBroadcaster;
        LaggyGridScanner _gridScanner;
        LaggyGridReportBuffer _gridReportBuffer;
        LaggyGridGpsCreator _gridCreator;

        ShittyShitShitterConfig Config => _config.Data;

        public bool Enabled
        {
            private get => Config.EnableBroadcasting;
            set => Config.EnableBroadcasting = value;
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.ListenOnGameLoaded(OnGameLoaded);
            this.ListenOnGameUnloading(OnGameUnloading);

            var mngr = new ShittyShitShitterManager(torch);
            torch.Managers.AddManager(mngr);

            var configFilePath = this.MakeConfigFilePath();
            _config = Persistent<ShittyShitShitterConfig>.Load(configFilePath);

            _canceller = new CancellationTokenSource();

            _gridScanner = new LaggyGridScanner(Config);
            _gridReportBuffer = new LaggyGridReportBuffer(Config);
            _gridCreator = new LaggyGridGpsCreator();
            _gpsBroadcaster = new GpsBroadcaster(Config, "LaggyGridGps");
        }

        UserControl IWpfPlugin.GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        void OnGameLoaded()
        {
            _canceller.StartAsync(LoopCollecting).Forget(Log);

            _gpsBroadcaster.CleanAllCustomGps();
            _canceller.StartAsync(_gpsBroadcaster.LoopCleaning).Forget(Log);
        }

        void LoopCollecting(CancellationToken canceller)
        {
            Log.Info("Started collector loop");

            // Idle for some time during the session startup
            canceller.WaitHandle.WaitOneSafe(Config.FirstIdleSeconds.Seconds());

            while (!canceller.IsCancellationRequested)
            {
                try
                {
                    if (!Enabled)
                    {
                        // clear past reports 
                        _gridReportBuffer.Clear();

                        if (!canceller.WaitHandle.WaitOneSafe(1.Seconds())) return;
                        continue;
                    }

                    RunOneInterval(canceller).Wait(canceller);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Error(e);

                    // wait a bit otherwise the logs will flood the UI
                    if (!canceller.WaitHandle.WaitOneSafe(5.Seconds())) return;
                }
            }
        }

        async Task RunOneInterval(CancellationToken canceller)
        {
            // profile laggy grids
            var gridReports = _gridScanner.ScanLaggyGrids(canceller);
            Log.Trace($"done scanning: {gridReports?.ToStringSeq()}");

            canceller.ThrowIfCancellationRequested();

            _gridReportBuffer.AddInterval(gridReports);

            // find "persistently laggy grids" over multiple intervals
            var targetGridIds = _gridReportBuffer.GetPersistentlyLaggyGridIds();

            // retrieve laggy grids by grid IDs
            var reportIdMapping = gridReports.ToDictionary(r => r.GridId);
            var targetGridReports = targetGridIds.Select(i => reportIdMapping[i]);

            // MyGps can be created in the game loop only (idk why)
            await GameLoopObserver.WaitUntilGameLoop();

            canceller.ThrowIfCancellationRequested();

            // collect GPS of laggy grids
            var gpsCollection = new List<MyGps>();
            foreach (var gridReport in targetGridReports)
            {
                var gpsOrNull = _gridCreator.CreateGpsOrNull(gridReport);
                if (gpsOrNull is MyGps gps)
                {
                    gpsCollection.Add(gps);
                }
            }

            await TaskUtils.MoveToThreadPool();

            canceller.ThrowIfCancellationRequested();

            // broadcast to players
            foreach (var laggyGridGps in gpsCollection)
            {
                _gpsBroadcaster.BroadcastToOnlinePlayers(laggyGridGps);
            }
        }

        void OnGameUnloading()
        {
            _canceller.Cancel();
            _canceller.Dispose();
        }

        public void CleanAllCustomGps()
        {
            _gpsBroadcaster.CleanAllCustomGps();
        }

        public IEnumerable<(long, MyGps)> GetAllCustomGpsEntities()
        {
            return _gpsBroadcaster.GetAllCustomGpsEntities();
        }

        public void MutePlayer(ulong playerSteamId)
        {
            Config.AddMutedPlayer(playerSteamId);
        }

        public void UnmutePlayer(ulong playerSteamId)
        {
            Config.RemoveMutedPlayer(playerSteamId);
        }
    }
}