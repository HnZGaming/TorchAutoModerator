using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AutoModerator.Core;
using AutoModerator.Grids;
using AutoModerator.Players;
using Sandbox.Game.World;
using Torch;
using Torch.Views;
using Utils.General;

namespace AutoModerator
{
    public sealed class AutoModeratorConfig :
        ViewModel,
        GridGpsSource.IConfig,
        BroadcastListenerCollection.IConfig,
        FileLoggingConfigurator.IConfig,
        LaggyGridTracker.IConfig,
        LaggyPlayerTracker.IConfig,
        PlayerGpsSource.IConfig
    {
        const string OpGroupName = "Auto Moderator";
        const string FuncGroupName = "Profiling & Broadcasting";
        const string GridFuncGroupName = FuncGroupName + " (Grids)";
        const string PlayerFuncGroupName = FuncGroupName + " (Players)";
        const string LogGroupName = "Logging";
        public const string DefaultLogFilePath = "Logs/AutoModerator-${shortdate}.log";

        double _firstIdle = 180;
        bool _enableBroadcasting = true;
        bool _enableGridBroadcasting = true;
        bool _enablePlayerBroadcasting = true;
        bool _adminsOnly = true;
        int _maxLaggyGpsCountPerScan = 3;
        double _gridPinWindow = 300d;
        double _gridPinLifespan = 600d;
        double _gridMspfThreshold = 3.0f;
        double _playerMspfThreshold = 3.0f;
        double _playerPinWindow = 300d;
        double _playerPinLifespan = 600d;
        double _sampleFrequency = 5;
        bool _exemptNpcFactions = true;
        string _gridGpsNameFormat = "[{faction}] {grid} {ratio} ({time})";
        string _gridGpsDescriptionFormat = "The {rank} laggiest grid. Get 'em!";
        string _playerGpsNameFormat = "[{faction}] {player} {ratio} ({time})";
        string _playerGpsDescriptionFormat = "The {rank} laggiest player. Get 'em!";
        string _gpsColor = "#FF00FF";
        List<ulong> _mutedPlayerIds = new List<ulong>();
        List<string> _exemptFactionTags = new List<string>();
        bool _suppressWpfOutput;
        bool _enableLoggingTrace;
        bool _enableLoggingDebug;
        string _logFilePath = DefaultLogFilePath;

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting", GroupName = OpGroupName)]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetValue(ref _enableBroadcasting, value);
        }

        [XmlElement("EnableGridBroadcasting")]
        [Display(Order = 0, Name = "Enable grid sample & broadcast", GroupName = GridFuncGroupName)]
        public bool EnableGridBroadcasting
        {
            get => _enableGridBroadcasting;
            set => SetValue(ref _enableGridBroadcasting, value);
        }

        [XmlElement("EnablePlayerBroadcasting")]
        [Display(Order = 0, Name = "Enable player sample & broadcast", GroupName = PlayerFuncGroupName)]
        public bool EnablePlayerBroadcasting
        {
            get => _enablePlayerBroadcasting;
            set => SetValue(ref _enablePlayerBroadcasting, value);
        }

        [XmlElement("EnableAdminsOnly")]
        [Display(Order = 1, Name = "Broadcast to admins only", GroupName = OpGroupName)]
        public bool AdminsOnly
        {
            get => _adminsOnly;
            set => SetValue(ref _adminsOnly, value);
        }

        [XmlElement("FirstIdleSeconds")]
        [Display(Order = 2, Name = "First idle seconds", GroupName = FuncGroupName, Description = "Game is generally laggy for the first minute or two of the session.")]
        public double FirstIdle
        {
            get => _firstIdle;
            set => SetValue(ref _firstIdle, value);
        }

        [XmlElement("GridMspfThreshold")]
        [Display(Order = 3, Name = "Grid ms/f threshold", GroupName = GridFuncGroupName)]
        public double GridMspfThreshold
        {
            get => _gridMspfThreshold;
            set => SetValue(ref _gridMspfThreshold, Math.Max(value, 0.001f));
        }

        [XmlElement("MaxGpsCount")]
        [Display(Order = 5, Name = "Max GPS count", GroupName = FuncGroupName)]
        public int MaxGpsCount
        {
            get => _maxLaggyGpsCountPerScan;
            set => SetValue(ref _maxLaggyGpsCountPerScan, value);
        }

        [XmlElement("SampleFrequencySeconds")]
        [Display(Order = 5, Name = "Sample frequency (seconds)", GroupName = FuncGroupName)]
        public double ProfileTime
        {
            get => _sampleFrequency;
            set => SetValue(ref _sampleFrequency, Math.Max(value, 1));
        }

        [XmlElement("GridPinWindow")]
        [Display(Order = 6, Name = "Sample time (seconds)", GroupName = GridFuncGroupName)]
        public double GridPinWindow
        {
            get => _gridPinWindow;
            set => SetValue(ref _gridPinWindow, value);
        }

        [XmlElement("GridPinLifespan")]
        [Display(Order = 7, Name = "Broadcast time (seconds)", GroupName = GridFuncGroupName)]
        public double GridPinLifespan
        {
            get => _gridPinLifespan;
            set => SetValue(ref _gridPinLifespan, value);
        }

        [XmlElement("GridGpsNameFormat")]
        [Display(Order = 8, Name = "GPS name format", GroupName = GridFuncGroupName)]
        public string GridGpsNameFormat
        {
            get => _gridGpsNameFormat;
            set => SetValue(ref _gridGpsNameFormat, value);
        }

        [XmlElement("GridGpsDescriptionFormat")]
        [Display(Order = 9, Name = "GPS description format", GroupName = GridFuncGroupName)]
        public string GridGpsDescriptionFormat
        {
            get => _gridGpsDescriptionFormat;
            set => SetValue(ref _gridGpsDescriptionFormat, value);
        }

        [XmlElement("PlayerMspfThreshold")]
        [Display(Order = 3, Name = "ms/f threshold", GroupName = PlayerFuncGroupName)]
        public double PlayerMspfThreshold
        {
            get => _playerMspfThreshold;
            set => SetValue(ref _playerMspfThreshold, value);
        }

        [XmlElement("PlayerPinWindow")]
        [Display(Order = 6, Name = "Sample time (seconds)", GroupName = PlayerFuncGroupName)]
        public double PlayerPinWindow
        {
            get => _playerPinWindow;
            set => SetValue(ref _playerPinWindow, value);
        }

        [XmlElement("PlayerPinLifespan")]
        [Display(Order = 7, Name = "Broadcast time (seconds)", GroupName = PlayerFuncGroupName)]
        public double PlayerPinLifespan
        {
            get => _playerPinLifespan;
            set => SetValue(ref _playerPinLifespan, value);
        }

        [XmlElement("PlayerGpsNameFormat")]
        [Display(Order = 8, Name = "GPS name format", GroupName = PlayerFuncGroupName)]
        public string PlayerGpsNameFormat
        {
            get => _playerGpsNameFormat;
            set => SetValue(ref _playerGpsNameFormat, value);
        }

        [XmlElement("PlayerGpsDescriptionFormat")]
        [Display(Order = 8, Name = "GPS description format", GroupName = PlayerFuncGroupName)]
        public string PlayerGpsDescriptionFormat
        {
            get => _playerGpsDescriptionFormat;
            set => SetValue(ref _playerGpsDescriptionFormat, value);
        }

        [XmlElement("GpsColor")]
        [Display(Order = 8, Name = "GPS text color", GroupName = FuncGroupName)]
        public string GpsColor
        {
            get => _gpsColor;
            set => SetValue(ref _gpsColor, value);
        }

        [XmlElement("IgnoreNpcFactions")]
        [Display(Order = 10, Name = "Ignore NPC factions", GroupName = OpGroupName)]
        public bool IgnoreNpcFactions
        {
            get => _exemptNpcFactions;
            set => SetValue(ref _exemptNpcFactions, value);
        }

        [XmlElement("ExemptFactionTags")]
        [Display(Order = 11, Name = "Exempt faction tags", GroupName = OpGroupName)]
        public List<string> ExemptFactionTags
        {
            get => _exemptFactionTags;
            set => SetValue(ref _exemptFactionTags, new HashSet<string>(value).ToList());
        }

        [XmlElement("MutedPlayerIds")]
        [Display(Order = 12, Name = "Muted players", GroupName = OpGroupName)]
        public List<ulong> MutedPlayerIds
        {
            get => _mutedPlayerIds;
            set => SetValue(ref _mutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        [XmlElement("SuppressWpfOutput")]
        [Display(Order = 12, Name = "Suppress Console Output", GroupName = LogGroupName)]
        public bool SuppressWpfOutput
        {
            get => _suppressWpfOutput;
            set => SetValue(ref _suppressWpfOutput, value);
        }

        [XmlElement("EnableLoggingTrace")]
        [Display(Order = 13, Name = "Enable Logging Trace", GroupName = LogGroupName)]
        public bool EnableLoggingTrace
        {
            get => _enableLoggingTrace;
            set => SetValue(ref _enableLoggingTrace, value);
        }

        [XmlElement("EnableLoggingDebug")]
        [Display(Order = 13, Name = "Enable Logging Debug", GroupName = LogGroupName)]
        public bool EnableLoggingDebug
        {
            get => _enableLoggingDebug;
            set => SetValue(ref _enableLoggingDebug, value);
        }

        [XmlElement("LogFilePath")]
        [Display(Order = 14, Name = "Log File Path", GroupName = LogGroupName)]
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetValue(ref _logFilePath, value);
        }

        IEnumerable<ulong> BroadcastListenerCollection.IConfig.MutedPlayers => _mutedPlayerIds;

        public void AddMutedPlayer(ulong mutedPlayerId)
        {
            if (!_mutedPlayerIds.Contains(mutedPlayerId))
            {
                _mutedPlayerIds.Add(mutedPlayerId);
                OnPropertyChanged(nameof(MutedPlayerIds));
            }
        }

        public void RemoveMutedPlayer(ulong unmutedPlayerId)
        {
            if (_mutedPlayerIds.Remove(unmutedPlayerId))
            {
                OnPropertyChanged(nameof(MutedPlayerIds));
            }
        }

        public void RemoveAllMutedPlayers()
        {
            _mutedPlayerIds.Clear();
            OnPropertyChanged(nameof(MutedPlayerIds));
        }

        public bool IsFactionExempt(string factionTag)
        {
            var exemptByNpc = MySession.Static.Factions.IsNpcFaction(factionTag) && IgnoreNpcFactions;
            var exemptByTag = ExemptFactionTags.Contains(factionTag.ToLower());
            return exemptByNpc || exemptByTag;
        }
    }
}