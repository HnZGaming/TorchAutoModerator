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
using Utils.Torch;

namespace AutoModerator
{
    public sealed class AutoModeratorConfig :
        ViewModel,
        EntityGpsBroadcaster.IConfig,
        BroadcastListenerCollection.IConfig,
        FileLoggingConfigurator.IConfig,
        GridLagTracker.IConfig,
        PlayerLagTracker.IConfig,
        LagWarningCollection.IConfig,
        LagPunishmentExecutor.IConfig
    {
        const string OpGroupName = "Auto Moderator";
        const string OpGridGroupName = "Auto Moderator (Grids)";
        const string OpPlayerGroupName = "Auto Moderator (Players)";
        const string BroadcastGroupName = "Punishment (Broadcast)";
        const string DamageGroupName = "Punishment (Damage)";
        const string WarningGroupName = "Warnings";
        const string PunishGroupName = "Punishment";
        const string LogGroupName = "_Logging_";
        public const string DefaultLogFilePath = "Logs/AutoModerator-${shortdate}.log";

        bool _enableWarning = true;
        double _firstIdleTime = 180;
        bool _broadcastAdminsOnly = true;
        int _maxLaggyGpsCountPerScan = 5;
        double _gridWarningTime = 300d;
        double _gridPunishTime = 600d;
        double _maxGridMspf = 0.5f;
        double _maxPlayerMspf = 0.5f;
        double _playerWarningTime = 300d;
        double _playerPunishTime = 600d;
        double _sampleFrequency = 5;
        double _warningNormal = 0.7d;
        bool _exemptNpcFactions = true;
        string _gridGpsNameFormat = "[{faction}] {grid} {ratio} ({time})";
        string _gridGpsDescriptionFormat = "The {rank} laggiest grid. Get 'em!";
        string _gpsColor = "#FF00FF";
        List<ulong> _mutedPlayerIds = new List<ulong>();
        List<string> _exemptFactionTags = new List<string>();
        bool _suppressWpfOutput;
        bool _enableLoggingTrace;
        bool _enableLoggingDebug;
        string _logFilePath = DefaultLogFilePath;
        string _warningTitle = LagWarningDefaultTexts.Title;
        string _warningDetailMustProfileSelfText = LagWarningDefaultTexts.MustProfileSelf;
        string _warningDetailMustDelagSelfText = LagWarningDefaultTexts.MustDelagSelf;
        string _warningDetailMustWaitUnpinnedText = LagWarningDefaultTexts.MustWaitUnpinned;
        string _warningDetailEndedText = LagWarningDefaultTexts.Ended;
        LagPunishmentType _punishmentType;
        double _damageNormal = 0.5d;
        string _warningCurrentLevelText = LagWarningDefaultTexts.CurrentLevel;

        [XmlElement(nameof(FirstIdleTime))]
        [Display(Order = 2, Name = "First idle seconds", GroupName = OpGroupName,
            Description = "Waits for N seconds when the session starts. Game is generally laggy at startup due to concealment or cleanup.")]
        public double FirstIdleTime
        {
            get => _firstIdleTime;
            set => SetValue(ref _firstIdleTime, value);
        }

        [XmlElement(nameof(MaxGridMspf))]
        [Display(Order = 3, Name = "Max grid ms/f", GroupName = OpGridGroupName,
            Description = "Allows N milliseconds per game loop for each grid to consume.")]
        public double MaxGridMspf
        {
            get => _maxGridMspf;
            set => SetValue(ref _maxGridMspf, Math.Max(value, 0.001f));
        }

        [XmlElement(nameof(IntervalFrequency))]
        [Display(Order = 5, Name = "Interval frequency (seconds)", GroupName = OpGroupName,
            Description = "Profiles N seconds per interval.")]
        public double IntervalFrequency
        {
            get => _sampleFrequency;
            set => SetValue(ref _sampleFrequency, Math.Max(value, 5));
        }

        [XmlElement(nameof(GridWarningTime))]
        [Display(Order = 6, Name = "Warning time (seconds)", GroupName = OpGridGroupName,
            Description = "Gives players a chance of N seconds before the punishment of per-grid lag violation.")]
        public double GridWarningTime
        {
            get => _gridWarningTime;
            set => SetValue(ref _gridWarningTime, value);
        }

        [XmlElement(nameof(GridPunishTime))]
        [Display(Order = 7, Name = "Pinned time (seconds)", GroupName = OpGridGroupName,
            Description = "Punishes players for N seconds for per-grid lag violation.")]
        public double GridPunishTime
        {
            get => _gridPunishTime;
            set => SetValue(ref _gridPunishTime, value);
        }

        [XmlElement(nameof(MaxPlayerMspf))]
        [Display(Order = 3, Name = "Max player ms/f", GroupName = OpPlayerGroupName,
            Description = "Allows N milliseconds per game loop for each player to consume.")]
        public double MaxPlayerMspf
        {
            get => _maxPlayerMspf;
            set => SetValue(ref _maxPlayerMspf, value);
        }

        [XmlElement(nameof(PlayerWarningTime))]
        [Display(Order = 6, Name = "Warning time (seconds)", GroupName = OpPlayerGroupName,
            Description = "Gives players a chance of N seconds before the punishment of per-player lag violation.")]
        public double PlayerWarningTime
        {
            get => _playerWarningTime;
            set => SetValue(ref _playerWarningTime, value);
        }

        [XmlElement(nameof(PlayerPunishTime))]
        [Display(Order = 7, Name = "Pinned time (seconds)", GroupName = OpPlayerGroupName,
            Description = "Punishes players for N seconds for per-player lag violation.")]
        public double PlayerPunishTime
        {
            get => _playerPunishTime;
            set => SetValue(ref _playerPunishTime, value);
        }

        [XmlElement(nameof(EnableWarning))]
        [Display(Order = 0, Name = "Enable warning", GroupName = WarningGroupName)]
        public bool EnableWarning
        {
            get => _enableWarning;
            set => SetValue(ref _enableWarning, value);
        }

        [XmlElement(nameof(WarningNormal))]
        [Display(Order = 1, Name = "Normal (0-1)", GroupName = WarningGroupName,
            Description = "Send a warning to players when they exceed N times the max allowed lag per grid or player.")]
        public double WarningNormal
        {
            get => _warningNormal;
            set => SetValue(ref _warningNormal, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningTitle))]
        [Display(Order = 2, Name = "Title", GroupName = WarningGroupName)]
        public string WarningTitle
        {
            get => _warningTitle;
            set => SetValue(ref _warningTitle, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningDetailMustProfileSelfText))]
        [Display(Order = 3, Name = "Detail (1)", GroupName = WarningGroupName)]
        public string WarningDetailMustProfileSelfText
        {
            get => _warningDetailMustProfileSelfText;
            set => SetValue(ref _warningDetailMustProfileSelfText, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningDetailMustDelagSelfText))]
        [Display(Order = 4, Name = "Detail (2)", GroupName = WarningGroupName)]
        public string WarningDetailMustDelagSelfText
        {
            get => _warningDetailMustDelagSelfText;
            set => SetValue(ref _warningDetailMustDelagSelfText, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningDetailMustWaitUnpinnedText))]
        [Display(Order = 5, Name = "Detail (3)", GroupName = WarningGroupName)]
        public string WarningDetailMustWaitUnpinnedText
        {
            get => _warningDetailMustWaitUnpinnedText;
            set => SetValue(ref _warningDetailMustWaitUnpinnedText, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningDetailEndedText))]
        [Display(Order = 6, Name = "Detail (4)", GroupName = WarningGroupName)]
        public string WarningDetailEndedText
        {
            get => _warningDetailEndedText;
            set => SetValue(ref _warningDetailEndedText, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(WarningCurrentLevelText))]
        [Display(Order = 7, Name = "Current level", GroupName = WarningGroupName)]
        public string WarningCurrentLevelText
        {
            get => _warningCurrentLevelText;
            set => SetValue(ref _warningCurrentLevelText, value);
        }

        [XmlElement(nameof(PunishmentType))]
        [Display(Order = 1, Name = "Punishment type", GroupName = PunishGroupName)]
        public LagPunishmentType PunishmentType
        {
            get => _punishmentType;
            set => SetValue(ref _punishmentType, value);
        }

        [XmlElement(nameof(DamageNormalPerInterval))]
        [Display(Order = 2, Name = "Damage per interval (0-1)", GroupName = DamageGroupName,
            Description = "Applies damage to subject blocks by N times the block type's max integrity.")]
        public double DamageNormalPerInterval
        {
            get => _damageNormal;
            set => SetValue(ref _damageNormal, value);
        }

        [XmlElement(nameof(BroadcastAdminsOnly))]
        [Display(Order = 5, Name = "Broadcast to admins only", GroupName = BroadcastGroupName,
            Description = "Broadcasts GPS of laggy grids to admin players only.")]
        public bool BroadcastAdminsOnly
        {
            get => _broadcastAdminsOnly;
            set => SetValue(ref _broadcastAdminsOnly, value);
        }

        [XmlElement(nameof(MaxGpsCount))]
        [Display(Order = 6, Name = "Max GPS count", GroupName = BroadcastGroupName,
            Description = "Shows N number of GPS of laggy grids on every player's HUD.")]
        public int MaxGpsCount
        {
            get => _maxLaggyGpsCountPerScan;
            set => SetValue(ref _maxLaggyGpsCountPerScan, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(GpsNameFormat))]
        [Display(Order = 7, Name = "GPS name format", GroupName = BroadcastGroupName)]
        public string GpsNameFormat
        {
            get => _gridGpsNameFormat;
            set => SetValue(ref _gridGpsNameFormat, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(GpsDescriptionFormat))]
        [Display(Order = 8, Name = "GPS description format", GroupName = BroadcastGroupName)]
        public string GpsDescriptionFormat
        {
            get => _gridGpsDescriptionFormat;
            set => SetValue(ref _gridGpsDescriptionFormat, value);
        }

        [XmlElement(nameof(GpsColorCode))]
        [Display(Order = 9, Name = "GPS text color", GroupName = BroadcastGroupName)]
        public string GpsColorCode
        {
            get => _gpsColor;
            set => SetValue(ref _gpsColor, value);
        }

        [XmlElement(nameof(IgnoreNpcFactions))]
        [Display(Order = 10, Name = "Ignore NPC factions", GroupName = OpGroupName)]
        public bool IgnoreNpcFactions
        {
            get => _exemptNpcFactions;
            set => SetValue(ref _exemptNpcFactions, value);
        }

        [XmlElement(nameof(ExemptFactionTags))]
        [Display(Order = 11, Name = "Exempt faction tags", GroupName = OpGroupName)]
        public List<string> ExemptFactionTags
        {
            get => _exemptFactionTags;
            set => SetValue(ref _exemptFactionTags, new HashSet<string>(value).ToList());
        }

        [XmlElement(nameof(MutedPlayerIds))]
        [Display(Order = 12, Name = "Muted players", GroupName = BroadcastGroupName)]
        public List<ulong> MutedPlayerIds
        {
            get => _mutedPlayerIds;
            set => SetValue(ref _mutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(SuppressWpfOutput))]
        [Display(Order = 12, Name = "Suppress Console Output", GroupName = LogGroupName)]
        public bool SuppressWpfOutput
        {
            get => _suppressWpfOutput;
            set => SetValue(ref _suppressWpfOutput, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(EnableLoggingTrace))]
        [Display(Order = 13, Name = "Enable Logging Trace", GroupName = LogGroupName)]
        public bool EnableLoggingTrace
        {
            get => _enableLoggingTrace;
            set => SetValue(ref _enableLoggingTrace, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(EnableLoggingDebug))]
        [Display(Order = 13, Name = "Enable Logging Debug", GroupName = LogGroupName)]
        public bool EnableLoggingDebug
        {
            get => _enableLoggingDebug;
            set => SetValue(ref _enableLoggingDebug, value);
        }

        [ConfigCommandIgnore]
        [XmlElement(nameof(LogFilePath))]
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