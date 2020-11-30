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
        LaggyGridCollector _gridCollector;
        LaggyGridWindowBuffer _gridBuffer;
        LaggyGridGpsBroadcaster _gridBroadcaster;

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

            var configFilePath = this.MakeConfigFilePath();
            _config = Persistent<ShittyShitShitterConfig>.Load(configFilePath);

            _canceller = new CancellationTokenSource();
            _gridCollector = new LaggyGridCollector(Config, _gridBuffer);
            _gridBroadcaster = new LaggyGridGpsBroadcaster(Config);
            _gridBuffer = new LaggyGridWindowBuffer(Config, _gridBroadcaster);
        }

        UserControl IWpfPlugin.GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        void OnGameLoaded()
        {
            _canceller.StartAsync(LoopCollecting).Forget(Log);

            _gridBroadcaster.CleanAllCustomGps();
            _canceller.StartAsync(_gridBroadcaster.LoopCleaning).Forget(Log);
        }

        void LoopCollecting(CancellationToken canceller)
        {
            Log.Info("Started collector loop");

            while (!canceller.IsCancellationRequested)
            {
                if (!Enabled)
                {
                    // clear past reports 
                    _gridBuffer.ResetCollection();

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
                    _gridCollector.CollectLaggyGrids(canceller);
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
            _gridBroadcaster.CleanAllCustomGps();
        }
    }
}