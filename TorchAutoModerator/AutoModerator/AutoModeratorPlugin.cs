﻿using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Controls;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Utils.General;
using Utils.Torch;
using Utils.Torch.Patches;

namespace AutoModerator
{
    public sealed class AutoModeratorPlugin : TorchPluginBase, IWpfPlugin
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        Persistent<AutoModeratorConfig> _config;
        UserControl _userControl;
        CancellationTokenSource _canceller;
        FileLoggingConfigurator _fileLoggingConfigurator;

        public AutoModeratorConfig Config => _config.Data;
        public Core.AutoModerator AutoModerator { get; private set; }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            this.OnSessionStateChanged(TorchSessionState.Loaded, OnGameLoaded);
            this.OnSessionStateChanged(TorchSessionState.Unloading, OnGameUnloading);

            var configFilePath = this.MakeFilePath($"{nameof(AutoModeratorPlugin)}.cfg");
            _config = Persistent<AutoModeratorConfig>.Load(configFilePath);
            _config.Data.Initialize();
            Config.PropertyChanged += OnConfigChanged;

            _fileLoggingConfigurator = new FileLoggingConfigurator(
                "AutoModerator",
                new[] { "AutoModerator.*", "Utils.TorchEntityGps.*", "Utils.TimeSerieses.*" },
                AutoModeratorConfig.DefaultLogFilePath);

            _fileLoggingConfigurator.Initialize();
            _fileLoggingConfigurator.Configure(Config);

            if (Config.DisableGpsMod)
            {
                Log.Info("Not adding GPS mod");
            }
            else
            {
                // Local Gps Mod
                ModAdditionPatch.AddModForServerAndClient(2781829620);
            }
        }

        UserControl IWpfPlugin.GetControl()
        {
            return _config.GetOrCreateUserControl(ref _userControl);
        }

        void OnGameLoaded()
        {
            var chatManager = Torch.CurrentSession.Managers.GetManager<IChatManagerServer>();
            chatManager.ThrowIfNull("chat manager not found");

            AutoModerator = new Core.AutoModerator(Config, chatManager);
            _canceller = new CancellationTokenSource();

            TaskUtils.RunUntilCancelledAsync(AutoModerator.Main, _canceller.Token).Forget(Log);
        }

        void OnGameUnloading()
        {
            Config.PropertyChanged -= OnConfigChanged;
            _config?.Dispose();
            _canceller?.Cancel();
            _canceller?.Dispose();
            AutoModerator?.Close();
        }

        void OnConfigChanged(object _, PropertyChangedEventArgs args)
        {
            _fileLoggingConfigurator.Configure(Config);
            Log.Info("config changed");
        }
    }
}