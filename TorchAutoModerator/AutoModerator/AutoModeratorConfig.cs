using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AutoModerator.Grids;
using AutoModerator.Players;
using AutoModerator.Punishes;
using AutoModerator.Punishes.Broadcasts;
using AutoModerator.Warnings;
using Sandbox.Game.World;
using Torch;
using Torch.Views;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

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
        LagPunishExecutor.IConfig,
        LagPunishChatFeed.IConfig
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
        MyPromoteLevel _broadcastVisiblePromoLevel = MyPromoteLevel.Admin;
        int _maxLaggyGpsCountPerScan = 3;
        double _trackingTime = 300d;
        double _punishTime = 600d;
        double _maxGridMspf = 0.5f;
        double _maxPlayerMspf = 0.5f;
        double _sampleFrequency = 5;
        double _warningNormal = 0.7d;
        bool _exemptNpcFactions = true;
        string _gridGpsNameFormat = "[{faction}] {grid} {ratio} ({time})";
        string _gridGpsDescriptionFormat = "The {rank} laggiest grid. Get 'em!";
        string _gpsColor = "#FF00FF";
        List<ulong> _gpsMutedPlayerIds = new List<ulong>();
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
        LagPunishType _punishType;
        double _damageNormal = 0.05d;
        string _warningCurrentLevelText = LagWarningDefaultTexts.CurrentLevel;
        double _minIntegrityNormal = 0.5d;
        bool _enablePunishChatFeed = true;
        string _punishReportChatName = "Auto Moderator";
        string _punishReportChatFormat = "[{faction}] {player} \"{grid}\" ({level})";
        double _outlierFenceNormal = 2;
        double _gracePeriodTime = 20;

        [XmlElement(nameof(FirstIdleTime))]
        [Display(Order = 2, Name = "First idle seconds", GroupName = OpGroupName,
            Description = "Waits for N seconds when the session starts. Game is generally laggy at startup due to concealment or cleanup.")]
        public double FirstIdleTime
        {
            get => _firstIdleTime;
            set => SetValue(ref _firstIdleTime, value);
        }

        [XmlElement(nameof(IntervalFrequency))]
        [Display(Order = 5, Name = "Interval frequency (seconds)", GroupName = OpGroupName,
            Description = "Profiles N seconds per interval.")]
        public double IntervalFrequency
        {
            get => _sampleFrequency;
            set => SetValue(ref _sampleFrequency, Math.Max(value, 5));
        }

        [ConfigProperty(ConfigPropertyType.VisibleToPlayers)]
        [XmlElement(nameof(TrackingTime))]
        [Display(Order = 6, Name = "Tracking time (seconds)", GroupName = OpGroupName,
            Description = "Gives players a chance of N seconds before the punishment of per-grid lag violation.")]
        public double TrackingTime
        {
            get => _trackingTime;
            set => SetValue(ref _trackingTime, value);
        }

        [ConfigProperty(ConfigPropertyType.VisibleToPlayers)]
        [XmlElement(nameof(PunishTime))]
        [Display(Order = 7, Name = "Pinned time (seconds)", GroupName = OpGroupName,
            Description = "Punishes players for N seconds for per-grid lag violation.")]
        public double PunishTime
        {
            get => _punishTime;
            set => SetValue(ref _punishTime, value);
        }
        
        [ConfigProperty(ConfigPropertyType.VisibleToPlayers)]
        [XmlElement(nameof(GracePeriodTime))]
        [Display(Order = 10, Name = "Grace period (seconds)", GroupName = OpGroupName,
            Description = "Grids younger than N seconds will not be warned/punished.")]
        public double GracePeriodTime
        {
            get => _gracePeriodTime;
            set => SetValue(ref _gracePeriodTime, value);
        }

        [XmlElement(nameof(OutlierFenceNormal))]
        [Display(Order = 20, Name = "Outlier fence normal", GroupName = OpGroupName,
            Description = "Ignores spontaneous lags (N times larger than the standard deviation) of given grid/player's timeline.")]
        public double OutlierFenceNormal
        {
            get => _outlierFenceNormal;
            set => SetValue(ref _outlierFenceNormal, value);
        }

        [XmlElement(nameof(IgnoreNpcFactions))]
        [Display(Order = 22, Name = "Ignore NPC factions", GroupName = OpGroupName)]
        public bool IgnoreNpcFactions
        {
            get => _exemptNpcFactions;
            set => SetValue(ref _exemptNpcFactions, value);
        }

        [XmlElement(nameof(ExemptFactionTags))]
        [Display(Order = 24, Name = "Exempt faction tags", GroupName = OpGroupName)]
        public List<string> ExemptFactionTags
        {
            get => _exemptFactionTags;
            set => SetValue(ref _exemptFactionTags, new HashSet<string>(value).ToList());
        }

        [XmlElement(nameof(MaxGridMspf))]
        [Display(Order = 3, Name = "Max grid ms/f", GroupName = OpGridGroupName,
            Description = "Allows N milliseconds per game loop for each grid to consume.")]
        public double MaxGridMspf
        {
            get => _maxGridMspf;
            set => SetValue(ref _maxGridMspf, Math.Max(value, 0.001f));
        }

        [XmlElement(nameof(MaxPlayerMspf))]
        [Display(Order = 3, Name = "Max player ms/f", GroupName = OpPlayerGroupName,
            Description = "Allows N milliseconds per game loop for each player to consume.")]
        public double MaxPlayerMspf
        {
            get => _maxPlayerMspf;
            set => SetValue(ref _maxPlayerMspf, value);
        }

        [XmlElement(nameof(EnableWarning))]
        [Display(Order = 0, Name = "Enable warning", GroupName = WarningGroupName)]
        public bool EnableWarning
        {
            get => _enableWarning;
            set => SetValue(ref _enableWarning, value);
        }

        [XmlElement(nameof(WarningLagNormal))]
        [Display(Order = 1, Name = "Lag threshold (0-1)", GroupName = WarningGroupName,
            Description = "Send a warning to players when they exceed N times the max allowed lag per grid or player.")]
        public double WarningLagNormal
        {
            get => _warningNormal;
            set => SetValue(ref _warningNormal, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningTitle))]
        [Display(Order = 2, Name = "Title", GroupName = WarningGroupName)]
        public string WarningTitle
        {
            get => _warningTitle;
            set => SetValue(ref _warningTitle, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningDetailMustProfileSelfText))]
        [Display(Order = 3, Name = "Detail (1)", GroupName = WarningGroupName)]
        public string WarningDetailMustProfileSelfText
        {
            get => _warningDetailMustProfileSelfText;
            set => SetValue(ref _warningDetailMustProfileSelfText, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningDetailMustDelagSelfText))]
        [Display(Order = 4, Name = "Detail (2)", GroupName = WarningGroupName)]
        public string WarningDetailMustDelagSelfText
        {
            get => _warningDetailMustDelagSelfText;
            set => SetValue(ref _warningDetailMustDelagSelfText, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningDetailMustWaitUnpinnedText))]
        [Display(Order = 5, Name = "Detail (3)", GroupName = WarningGroupName)]
        public string WarningDetailMustWaitUnpinnedText
        {
            get => _warningDetailMustWaitUnpinnedText;
            set => SetValue(ref _warningDetailMustWaitUnpinnedText, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningDetailEndedText))]
        [Display(Order = 6, Name = "Detail (4)", GroupName = WarningGroupName)]
        public string WarningDetailEndedText
        {
            get => _warningDetailEndedText;
            set => SetValue(ref _warningDetailEndedText, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(WarningCurrentLevelText))]
        [Display(Order = 7, Name = "Current level", GroupName = WarningGroupName)]
        public string WarningCurrentLevelText
        {
            get => _warningCurrentLevelText;
            set => SetValue(ref _warningCurrentLevelText, value);
        }

        [ConfigProperty(ConfigPropertyType.VisibleToPlayers)]
        [XmlElement(nameof(PunishType))]
        [Display(Order = 1, Name = "Punishment type", GroupName = PunishGroupName)]
        public LagPunishType PunishType
        {
            get => _punishType;
            set => SetValue(ref _punishType, value);
        }

        [XmlElement(nameof(EnablePunishChatFeed))]
        [Display(Order = 2, Name = "Enable punishment chat", GroupName = PunishGroupName)]
        public bool EnablePunishChatFeed
        {
            get => _enablePunishChatFeed;
            set => SetValue(ref _enablePunishChatFeed, value);
        }

        [XmlElement(nameof(PunishReportChatName))]
        [Display(Order = 3, Name = "Chat name", GroupName = PunishGroupName)]
        public string PunishReportChatName
        {
            get => _punishReportChatName;
            set => SetValue(ref _punishReportChatName, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(PunishReportChatFormat))]
        [Display(Order = 4, Name = "Chat format", GroupName = PunishGroupName)]
        public string PunishReportChatFormat
        {
            get => _punishReportChatFormat;
            set => SetValue(ref _punishReportChatFormat, value);
        }

        [XmlElement(nameof(DamageNormalPerInterval))]
        [Display(Order = 2, Name = "Damage per interval (0-1)", GroupName = DamageGroupName,
            Description = "Applies damage to subject blocks by N times the block type's max integrity.")]
        public double DamageNormalPerInterval
        {
            get => _damageNormal;
            set => SetValue(ref _damageNormal, value);
        }

        [XmlElement(nameof(MinIntegrityNormal))]
        [Display(Order = 2, Name = "Lowest integrity (0-1)", GroupName = DamageGroupName,
            Description = "Applies damage to subject blocks until reaching N times integrity.")]
        public double MinIntegrityNormal
        {
            get => _minIntegrityNormal;
            set => SetValue(ref _minIntegrityNormal, value);
        }

        [XmlElement(nameof(GpsVisiblePromoteLevel))]
        [Display(Order = 5, Name = "Broadcast visible promo level", GroupName = BroadcastGroupName,
            Description = "Broadcasts GPS to permitted players only.")]
        public MyPromoteLevel GpsVisiblePromoteLevel
        {
            get => _broadcastVisiblePromoLevel;
            set => SetValue(ref _broadcastVisiblePromoLevel, value);
        }

        [ConfigProperty(ConfigPropertyType.VisibleToPlayers)]
        [XmlElement(nameof(MaxGpsCount))]
        [Display(Order = 6, Name = "Max GPS count", GroupName = BroadcastGroupName,
            Description = "Shows N number of GPS of laggy grids on every player's HUD.")]
        public int MaxGpsCount
        {
            get => _maxLaggyGpsCountPerScan;
            set => SetValue(ref _maxLaggyGpsCountPerScan, value);
        }

        [XmlElement(nameof(GpsMutedPlayerIds))]
        [Display(Order = 12, Name = "Muted players", GroupName = BroadcastGroupName,
            Description = "Won't send chat or GPS to muted players.")]
        public List<ulong> GpsMutedPlayerIds
        {
            get => _gpsMutedPlayerIds;
            set => SetValue(ref _gpsMutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(GpsNameFormat))]
        [Display(Order = 17, Name = "GPS name format", GroupName = BroadcastGroupName)]
        public string GpsNameFormat
        {
            get => _gridGpsNameFormat;
            set => SetValue(ref _gridGpsNameFormat, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(GpsDescriptionFormat))]
        [Display(Order = 18, Name = "GPS description format", GroupName = BroadcastGroupName)]
        public string GpsDescriptionFormat
        {
            get => _gridGpsDescriptionFormat;
            set => SetValue(ref _gridGpsDescriptionFormat, value);
        }

        [XmlElement(nameof(GpsColorCode))]
        [Display(Order = 19, Name = "GPS text color", GroupName = BroadcastGroupName)]
        public string GpsColorCode
        {
            get => _gpsColor;
            set => SetValue(ref _gpsColor, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(SuppressWpfOutput))]
        [Display(Order = 12, Name = "Suppress Console Output", GroupName = LogGroupName)]
        public bool SuppressWpfOutput
        {
            get => _suppressWpfOutput;
            set => SetValue(ref _suppressWpfOutput, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(EnableLoggingTrace))]
        [Display(Order = 13, Name = "Enable Logging Trace", GroupName = LogGroupName)]
        public bool EnableLoggingTrace
        {
            get => _enableLoggingTrace;
            set => SetValue(ref _enableLoggingTrace, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(EnableLoggingDebug))]
        [Display(Order = 13, Name = "Enable Logging Debug", GroupName = LogGroupName)]
        public bool EnableLoggingDebug
        {
            get => _enableLoggingDebug;
            set => SetValue(ref _enableLoggingDebug, value);
        }

        [ConfigPropertyIgnore]
        [XmlElement(nameof(LogFilePath))]
        [Display(Order = 14, Name = "Log File Path", GroupName = LogGroupName)]
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetValue(ref _logFilePath, value);
        }

        IEnumerable<ulong> BroadcastListenerCollection.IConfig.GpsMutedPlayers => _gpsMutedPlayerIds;

        public void AddMutedPlayer(ulong mutedPlayerId)
        {
            if (!_gpsMutedPlayerIds.Contains(mutedPlayerId))
            {
                _gpsMutedPlayerIds.Add(mutedPlayerId);
                OnPropertyChanged(nameof(GpsMutedPlayerIds));
            }
        }

        public void RemoveMutedPlayer(ulong unmutedPlayerId)
        {
            if (_gpsMutedPlayerIds.Remove(unmutedPlayerId))
            {
                OnPropertyChanged(nameof(GpsMutedPlayerIds));
            }
        }

        public void RemoveAllMutedPlayers()
        {
            _gpsMutedPlayerIds.Clear();
            OnPropertyChanged(nameof(GpsMutedPlayerIds));
        }

        public bool IsFactionExempt(long factionId)
        {
            if (IgnoreNpcFactions && MySession.Static.Factions.IsNpcFaction(factionId)) return true;

            var factionTag = MySession.Static.Factions.TryGetFactionById(factionId)?.Tag;
            if (factionTag == null) return false;

            return ExemptFactionTags.Contains(factionTag.ToLower());
        }
    }
}