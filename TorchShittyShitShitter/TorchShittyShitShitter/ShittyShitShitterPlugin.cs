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
using TorchShittyShitShitter.Core.Scanners;
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
        LaggyGridFinder _gridFinder;
        LaggyGridReportBuffer _gridReportBuffer;
        LaggyGridGpsCreator _gridCreator;
        FactionScanner _factionScanner;
        ServerLagObserver _serverLagObserver;

        ShittyShitShitterConfig Config => _config.Data;

        public bool Enabled
        {
            private get => Config.EnableBroadcasting;
            set => Config.EnableBroadcasting = value;
        }

        public double MspfThreshold
        {
            get => Config.MspfPerOnlineGroupMember;
            set => Config.MspfPerOnlineGroupMember = value;
        }

        public double SimSpeedThreshold
        {
            get => Config.SimSpeedThreshold;
            set => Config.SimSpeedThreshold = value;
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

            _factionScanner = new FactionScanner(Config);

            _gridFinder = new LaggyGridFinder(Config, new ILagScanner[]
            {
                _factionScanner,
                new SinglePlayerScanner(Config),
                new UnownedGridScanner(Config),
            });

            _gridReportBuffer = new LaggyGridReportBuffer(Config);
            _gridCreator = new LaggyGridGpsCreator();
            _gpsBroadcaster = new GpsBroadcaster(Config);

            _serverLagObserver = new ServerLagObserver(Config, 5);
        }

        UserControl IWpfPlugin.GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        void OnGameLoaded()
        {
            _canceller.Token.RunUntilCancelledAsync(LoopCollecting).Forget(Log);
            _canceller.Token.RunUntilCancelledAsync(_factionScanner.LoopProfilingFactions).Forget(Log);
            _canceller.Token.RunUntilCancelledAsync(_gpsBroadcaster.LoopCleaning).Forget(Log);
            _canceller.Token.RunUntilCancelledAsync(_serverLagObserver.LoopObserving).Forget(Log);
        }

        async Task LoopCollecting(CancellationToken canceller)
        {
            Log.Info("Started collector loop");

            // Idle for some time during the session startup
            await canceller.Delay(Config.FirstIdleSeconds.Seconds());

            while (!canceller.IsCancellationRequested)
            {
                try
                {
                    if (!Enabled || !_serverLagObserver.IsLaggy)
                    {
                        // clear past reports 
                        _gridReportBuffer.Clear();

                        await canceller.Delay(1.Seconds());
                        continue;
                    }

                    await RunOneInterval(canceller);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Log.Error(e);

                    // wait a bit otherwise the logs will flood the UI
                    await canceller.Delay(5.Seconds());
                }
            }
        }

        async Task RunOneInterval(CancellationToken canceller)
        {
            // profile laggy grids
            var gridReports = await _gridFinder.ScanLaggyGrids(canceller);

            canceller.ThrowIfCancellationRequested();

            _gridReportBuffer.AddInterval(gridReports);

            // find "persistently laggy grids" over multiple intervals
            var targetGridIds = _gridReportBuffer.GetPersistentlyLaggyGridIds();

            // retrieve laggy grids by grid IDs
            var reportIdMapping = gridReports.ToDictionary(r => r.GridId);
            var targetGridReports =
                targetGridIds
                    .Select(i => reportIdMapping[i])
                    .OrderByDescending(r => r.Mspf);

            // MyGps can be created in the game loop only (idk why)
            await GameLoopObserver.WaitUntilGameLoop();

            canceller.ThrowIfCancellationRequested();

            // create GPS entities of laggy grids
            var gpsCollection = new List<MyGps>();
            foreach (var (gridReport, i) in targetGridReports.Select((r, i) => (r, i)))
            {
                var lagRank = i + 1;
                if (_gridCreator.TryCreateGps(gridReport.GridId, lagRank, out var gps))
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
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
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

        public void UnmuteAll()
        {
            Config.RemoveAllMutedPlayers();
        }
    }
}