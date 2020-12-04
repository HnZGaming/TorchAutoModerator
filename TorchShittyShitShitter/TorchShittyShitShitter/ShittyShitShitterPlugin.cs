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
using VRage.Game.ModAPI;

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
        LaggyGridGpsMaker _gridMaker;
        FactionScanner _factionScanner;
        ServerLagObserver _serverLagObserver;
        FactionMemberProfiler _factionMemberProfiler;
        LaggyGridGpsDescriptionMaker _descriptionMaker;

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

        public bool AdminsOnly
        {
            get => Config.AdminsOnly;
            set => Config.AdminsOnly = value;
        }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.ListenOnGameLoaded(OnGameLoaded);
            this.ListenOnGameUnloading(OnGameUnloading);

            GameLoopObserverManager.Add(torch);

            var configFilePath = this.MakeConfigFilePath();
            _config = Persistent<ShittyShitShitterConfig>.Load(configFilePath);

            _canceller = new CancellationTokenSource();

            _factionMemberProfiler = new FactionMemberProfiler();
            _factionScanner = new FactionScanner(Config, _factionMemberProfiler);

            _gridFinder = new LaggyGridFinder(Config, new ILagScanner[]
            {
                _factionScanner,
                new SinglePlayerScanner(Config),
                new UnownedGridScanner(Config),
            });

            _gridReportBuffer = new LaggyGridReportBuffer(Config);
            _descriptionMaker = new LaggyGridGpsDescriptionMaker(Config);
            _gridMaker = new LaggyGridGpsMaker(_descriptionMaker);
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
            await Task.Delay(Config.FirstIdleSeconds.Seconds(), canceller);

            while (!canceller.IsCancellationRequested)
            {
                try
                {
                    if (!Enabled || !_serverLagObserver.IsLaggy)
                    {
                        // clear past reports 
                        _gridReportBuffer.Clear();

                        await Task.Delay(1.Seconds(), canceller);
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
                    await Task.Delay(5.Seconds(), canceller);
                }
            }
        }

        async Task RunOneInterval(CancellationToken canceller)
        {
            var gridReports = await FindLaggyGrids(10.Seconds(), true, canceller);
            await BroadcastLaggyGrids(gridReports, canceller);
        }

        void OnGameUnloading()
        {
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
        }

        public async Task<IEnumerable<LaggyGridReport>> FindLaggyGrids(TimeSpan profileTime, bool buffered, CancellationToken canceller = default)
        {
            // profile laggy grids
            var gridReports = await _gridFinder.ScanLaggyGrids(profileTime, canceller);

            // put them in the buffer
            _gridReportBuffer.AddInterval(gridReports);

            if (buffered)
            {
                // find "persistently laggy grids" over multiple intervals
                var gridIds = _gridReportBuffer.GetPersistentlyLaggyGridIds();

                // retrieve laggy grids by grid IDs
                var reportIdMapping = gridReports.ToDictionary(r => r.GridId);
                gridReports = gridIds.Select(i => reportIdMapping[i]).OrderByDescending(r => r.Mspf);
            }

            return gridReports;
        }

        public async Task BroadcastLaggyGrids(IEnumerable<LaggyGridReport> gridReports, CancellationToken canceller = default)
        {
            // MyGps can be created in the game loop only (idk why)
            await GameLoopObserver.MoveToGameLoop(canceller);

            // create GPS entities of laggy grids
            var gpsCollection = new List<MyGps>();
            foreach (var (gridReport, i) in gridReports.Select((r, i) => (r, i)))
            {
                var lagRank = i + 1;
                if (_gridMaker.TryMakeGps(gridReport, lagRank, out var gps))
                {
                    gpsCollection.Add(gps);
                }
            }

            await TaskUtils.MoveToThreadPool(canceller);

            // broadcast to players
            foreach (var laggyGridGps in gpsCollection)
            {
                _gpsBroadcaster.BroadcastToOnlinePlayers(laggyGridGps);
            }
        }

        public void CleanAllCustomGps()
        {
            _gpsBroadcaster.DeleteAllCustomGps();
        }

        public IEnumerable<MyGps> GetAllCustomGpsEntities()
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

        public Task<IEnumerable<(IMyFaction Faction, int Count, double Mspf)>> ProfileFactionMembers(TimeSpan profileTime)
        {
            return _factionMemberProfiler.Profile(profileTime);
        }
    }
}