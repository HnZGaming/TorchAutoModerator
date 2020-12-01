using System;
using System.Threading;
using System.Windows.Controls;
using NLog;
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
        LaggyGridReportBuffer _gridBuffer;
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
            
            _gpsBroadcaster = new GpsBroadcaster(Config, "LaggyGridGps");
            _gridCreator = new LaggyGridGpsCreator(_gpsBroadcaster);
            _gridBuffer = new LaggyGridReportBuffer(Config, _gridCreator);
            _gridScanner = new LaggyGridScanner(Config, _gridBuffer);
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
            try
            {
                canceller.WaitHandle.WaitOne(Config.FirstIdleSeconds.Seconds());
            }
            catch // on cancellation
            {
                return;
            }

            while (!canceller.IsCancellationRequested)
            {
                if (!Enabled)
                {
                    // clear past reports 
                    _gridBuffer.Clear();

                    // dry run until re-enabled
                    try
                    {
                        canceller.WaitHandle.WaitOne(1.Seconds());
                        continue;
                    }
                    catch
                    {
                        return;
                    }
                }

                try
                {
                    // will spend several seconds here
                    _gridScanner.ScanLaggyGrids(canceller);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
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
    }
}