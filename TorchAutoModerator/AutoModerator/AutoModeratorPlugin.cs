using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using AutoModerator.Broadcast;
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

        LaggyGridTracker _laggyGridTracker;
        LaggyPlayerTracker _laggyPlayerTracker;
        LaggyEntityBroadcaster _laggyEntityBroadcaster;
        BroadcastListenerCollection _gpsReceivers;

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
            _laggyGridTracker = new LaggyGridTracker(Config);
            _laggyPlayerTracker = new LaggyPlayerTracker(Config);

            _gpsReceivers = new BroadcastListenerCollection(Config);
            _laggyEntityBroadcaster = new LaggyEntityBroadcaster(_gpsReceivers);
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

            _laggyEntityBroadcaster.ClearAllGpss();

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
                    playerProfiler.MarkStart();
                    Log.Debug("auto-profiling...");
                    await Task.Delay(Config.ProfileTime.Seconds(), canceller);
                    Log.Debug("auto-profile done");

                    // grids
                    var gridProfileResult = gridProfiler.GetResult();
                    _laggyGridTracker.Update(gridProfileResult);
                    Log.Debug($"{_laggyGridTracker.LastLaggyEntityCount} laggy grids");
                    Log.Debug($"{_laggyGridTracker.PinnedEntityCount} pinned grids");

                    // players
                    var playerProfileResult = playerProfiler.GetResult();
                    _laggyPlayerTracker.Update(playerProfileResult, gridProfileResult);
                    Log.Debug($"{_laggyPlayerTracker.LastLaggyEntityCount} laggy players");
                    Log.Debug($"{_laggyPlayerTracker.PinnedEntityCount} pinned players");
                }

                if (Config.EnableAlert)
                {
                }

                if (Config.EnableBroadcasting)
                {
                    _laggyEntityBroadcaster.InitializeInterval();

                    if (Config.EnablePlayerBroadcasting)
                    {
                        var gpsSources = _laggyPlayerTracker.CreateGpsSources(Config, Config.MaxGpsCount);
                        _laggyEntityBroadcaster.AddGpsSourceRange(gpsSources);
                    }

                    if (Config.EnableGridBroadcasting)
                    {
                        var gpsSources = _laggyGridTracker.CreateGpsSources(Config, Config.MaxGpsCount);
                        _laggyEntityBroadcaster.AddGpsSourceRange(gpsSources);
                    }

                    await _laggyEntityBroadcaster.SendIntervalGpss(Config.MaxGpsCount, canceller);
                }
                else
                {
                    _laggyEntityBroadcaster.ClearAllGpss();
                    Log.Debug("deleted all tracked gpss");
                }

                await TaskUtils.DelayMax(stopwatch, 1.Seconds(), canceller);
            }
        }

        public bool CheckPlayerReceivesGpss(MyPlayer player)
        {
            return _gpsReceivers.CheckReceive(player);
        }

        public void DeleteAllGpss()
        {
            _laggyEntityBroadcaster.ClearAllGpss();
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _laggyEntityBroadcaster.GetAllGpss();
        }
    }
}