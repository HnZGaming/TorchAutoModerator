using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AutoModerator.Broadcasts;
using AutoModerator.Grids;
using AutoModerator.Players;
using AutoModerator.Punishes;
using AutoModerator.Warnings;
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
        PlayerGpsSource.IConfig,
        GridLagTracker.IConfig,
        PlayerLagTracker.IConfig,
        LagWarningCollection.IConfig,
        LagPunishmentExecutor.IConfig
    {
        const string OpGroupName = "Auto Moderator";
        const string OpGridGroupName = "Auto Moderator (Grids)";
        const string OpPlayerGroupName = "Auto Moderator (Players)";
        const string BroadcastGroupName = "Broadcasting (General)";
        const string GridBroadcastGroupName = "Broadcasting (Grids)";
        const string PlayerBroadcastGroupName = "Broadcasting (Players)";
        const string WarningGroupName = "Warnings";
        const string PunishGroupName = "Punishment";
        const string LogGroupName = "Logging";
        public const string DefaultLogFilePath = "Logs/AutoModerator-${shortdate}.log";

        bool _enableWarning = true;
        double _firstIdleTime = 180;
        bool _enableGridBroadcasting = true;
        bool _enablePlayerBroadcasting = true;
        bool _adminsOnly = true;
        int _maxLaggyGpsCountPerScan = 5;
        double _gridWarningTime = 300d;
        double _gridPinTime = 600d;
        double _maxGridMspf = 0.5f;
        double _maxPlayerMspf = 0.5f;
        double _playerWarningTime = 300d;
        double _playerPinTime = 600d;
        double _sampleFrequency = 5;
        double _warningNormal = 0.7d;
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
        string _warningTitle = LagWarningDefaultTexts.Title;
        string _warningDetailMustProfileSelf = LagWarningDefaultTexts.MustProfileSelf;
        string _warningDetailMustDelagSelf = LagWarningDefaultTexts.MustDelagSelf;
        string _warningDetailMustWaitUnpinned = LagWarningDefaultTexts.MustWaitUnpinned;
        string _warningDetailEnded = LagWarningDefaultTexts.Ended;
        LagPunishmentType _punishmentType;
        double _damageNormal = 0.5d;
        double _punishmentInitialIdleTime = 300d;

        [XmlElement("EnableGridBroadcasting")]
        [Display(Order = 0, Name = "Enable grid broadcast", GroupName = GridBroadcastGroupName)]
        public bool EnableGridBroadcasting
        {
            get => _enableGridBroadcasting;
            set => SetValue(ref _enableGridBroadcasting, value);
        }

        [XmlElement("EnablePlayerBroadcasting")]
        [Display(Order = 0, Name = "Enable player broadcast", GroupName = PlayerBroadcastGroupName)]
        public bool EnablePlayerBroadcasting
        {
            get => _enablePlayerBroadcasting;
            set => SetValue(ref _enablePlayerBroadcasting, value);
        }

        [XmlElement("AdminsOnly")]
        [Display(Order = 1, Name = "Broadcast to admins only", GroupName = BroadcastGroupName)]
        public bool AdminsOnly
        {
            get => _adminsOnly;
            set => SetValue(ref _adminsOnly, value);
        }

        [XmlElement("FirstIdleTime")]
        [Display(Order = 2, Name = "First idle seconds", GroupName = OpGroupName, Description = "Game is generally laggy for the first minute or two of the session.")]
        public double FirstIdleTime
        {
            get => _firstIdleTime;
            set => SetValue(ref _firstIdleTime, value);
        }

        [XmlElement("MaxGridMspf")]
        [Display(Order = 3, Name = "Max grid ms/f", GroupName = OpGridGroupName)]
        public double MaxGridMspf
        {
            get => _maxGridMspf;
            set => SetValue(ref _maxGridMspf, Math.Max(value, 0.001f));
        }

        [XmlElement("MaxGpsCount")]
        [Display(Order = 5, Name = "Max GPS count", GroupName = BroadcastGroupName)]
        public int MaxGpsCount
        {
            get => _maxLaggyGpsCountPerScan;
            set => SetValue(ref _maxLaggyGpsCountPerScan, value);
        }

        [XmlElement("IntervalFrequency")]
        [Display(Order = 5, Name = "Interval frequency (seconds)", GroupName = OpGroupName)]
        public double ProfileFrequency
        {
            get => _sampleFrequency;
            set => SetValue(ref _sampleFrequency, Math.Max(value, 1));
        }

        [XmlElement("GridWarningTime")]
        [Display(Order = 6, Name = "Warning time (seconds)", GroupName = OpGridGroupName)]
        public double GridWarningTime
        {
            get => _gridWarningTime;
            set => SetValue(ref _gridWarningTime, value);
        }

        [XmlElement("GridPinTime")]
        [Display(Order = 7, Name = "Pinned time (seconds)", GroupName = OpGridGroupName)]
        public double GridPinTime
        {
            get => _gridPinTime;
            set => SetValue(ref _gridPinTime, value);
        }

        [XmlElement("GridGpsNameFormat")]
        [Display(Order = 8, Name = "GPS name format", GroupName = GridBroadcastGroupName)]
        public string GridGpsNameFormat
        {
            get => _gridGpsNameFormat;
            set => SetValue(ref _gridGpsNameFormat, value);
        }

        [XmlElement("GridGpsDescriptionFormat")]
        [Display(Order = 9, Name = "GPS description format", GroupName = GridBroadcastGroupName)]
        public string GridGpsDescriptionFormat
        {
            get => _gridGpsDescriptionFormat;
            set => SetValue(ref _gridGpsDescriptionFormat, value);
        }

        [XmlElement("MaxPlayerMspf")]
        [Display(Order = 3, Name = "Max player ms/f", GroupName = OpPlayerGroupName)]
        public double MaxPlayerMspf
        {
            get => _maxPlayerMspf;
            set => SetValue(ref _maxPlayerMspf, value);
        }

        [XmlElement("PlayerWarningTime")]
        [Display(Order = 6, Name = "Warning time (seconds)", GroupName = OpPlayerGroupName)]
        public double PlayerWarningTime
        {
            get => _playerWarningTime;
            set => SetValue(ref _playerWarningTime, value);
        }

        [XmlElement("PlayerPinTime")]
        [Display(Order = 7, Name = "Pinned time (seconds)", GroupName = OpPlayerGroupName)]
        public double PlayerPinTime
        {
            get => _playerPinTime;
            set => SetValue(ref _playerPinTime, value);
        }

        [XmlElement("PlayerGpsNameFormat")]
        [Display(Order = 8, Name = "GPS name format", GroupName = PlayerBroadcastGroupName)]
        public string PlayerGpsNameFormat
        {
            get => _playerGpsNameFormat;
            set => SetValue(ref _playerGpsNameFormat, value);
        }

        [XmlElement("PlayerGpsDescriptionFormat")]
        [Display(Order = 8, Name = "GPS description format", GroupName = PlayerBroadcastGroupName)]
        public string PlayerGpsDescriptionFormat
        {
            get => _playerGpsDescriptionFormat;
            set => SetValue(ref _playerGpsDescriptionFormat, value);
        }

        [XmlElement("GpsColor")]
        [Display(Order = 8, Name = "GPS text color", GroupName = BroadcastGroupName)]
        public string GpsColorCode
        {
            get => _gpsColor;
            set => SetValue(ref _gpsColor, value);
        }

        [XmlElement("EnableWarning")]
        [Display(Order = 0, Name = "Enable warning", GroupName = WarningGroupName)]
        public bool EnableWarning
        {
            get => _enableWarning;
            set => SetValue(ref _enableWarning, value);
        }

        [XmlElement("WarningNormal")]
        [Display(Order = 1, Name = "Warning normal (0-1)", GroupName = WarningGroupName)]
        public double WarningNormal
        {
            get => _warningNormal;
            set => SetValue(ref _warningNormal, value);
        }

        [XmlElement("WarningTitle")]
        [Display(Order = 2, Name = "Warning title", GroupName = WarningGroupName)]
        public string WarningTitle
        {
            get => _warningTitle;
            set => SetValue(ref _warningTitle, value);
        }

        [XmlElement("WarningDetailMustProfileSelf")]
        [Display(Order = 3, Name = "Warning title", GroupName = WarningGroupName)]
        public string WarningDetailMustProfileSelf
        {
            get => _warningDetailMustProfileSelf;
            set => SetValue(ref _warningDetailMustProfileSelf, value);
        }

        [XmlElement("WarningDetailMustDelagSelf")]
        [Display(Order = 4, Name = "Warning title", GroupName = WarningGroupName)]
        public string WarningDetailMustDelagSelf
        {
            get => _warningDetailMustDelagSelf;
            set => SetValue(ref _warningDetailMustDelagSelf, value);
        }

        [XmlElement("WarningDetailMustWaitUnpinned")]
        [Display(Order = 5, Name = "Warning title", GroupName = WarningGroupName)]
        public string WarningDetailMustWaitUnpinned
        {
            get => _warningDetailMustWaitUnpinned;
            set => SetValue(ref _warningDetailMustWaitUnpinned, value);
        }

        [XmlElement("WarningDetailEnded")]
        [Display(Order = 6, Name = "Warning title", GroupName = WarningGroupName)]
        public string WarningDetailEnded
        {
            get => _warningDetailEnded;
            set => SetValue(ref _warningDetailEnded, value);
        }

        [XmlElement("PunishmentInitialIdleTime")]
        [Display(Order = 0, Name = "First idle time (seconds)", GroupName = PunishGroupName)]
        public double PunishmentInitialIdleTime
        {
            get => _punishmentInitialIdleTime;
            set => SetValue(ref _punishmentInitialIdleTime, value);
        }

        [XmlElement("PunishmentType")]
        [Display(Order = 1, Name = "Punishment type", GroupName = PunishGroupName)]
        public LagPunishmentType PunishmentType
        {
            get => _punishmentType;
            set => SetValue(ref _punishmentType, value);
        }

        [XmlElement("DamageNormal")]
        [Display(Order = 2, Name = "Damage normal (0-1)", GroupName = PunishGroupName)]
        public double DamageNormal
        {
            get => _damageNormal;
            set => SetValue(ref _damageNormal, value);
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
        [Display(Order = 12, Name = "Muted players", GroupName = BroadcastGroupName)]
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