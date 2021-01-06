using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AutoModerator.Core;
using Torch;
using Torch.Views;
using Utils.General;

namespace AutoModerator
{
    public sealed class AutoModeratorConfig :
        ViewModel,
        GridReporter.IConfig,
        ServerLagObserver.IConfig,
        GridReportDescriber.IConfig,
        TargetPlayerCollector.IConfig,
        LaggyGridScanner.IConfig,
        FileLoggingConfigurator.IConfig
    {
        const string OpGroupName = "Auto Moderator";
        const string LogGroupName = "Logging";
        public const string DefaultLogFilePath = "Logs/AutoModerator-${shortdate}.log";

        double _firstIdleSeconds = 180;
        bool _enableBroadcasting = true;
        bool _adminsOnly = true;
        int _maxLaggyGridCountPerScan = 3;
        double _bufferSeconds = 300d;
        float _mspfThreshold = 3.0f;
        double _simSpeedThreshold = 0.7;
        bool _exemptNpcFactions = true;
        string _gpsDescriptionFormat = "The {rank} laggiest grid. Get 'em!";
        string _gpsNameFormat = "{grid} [{faction}] {ratio}";
        List<ulong> _mutedPlayerIds = new List<ulong>();
        List<string> _exemptFactionTags = new List<string>();
        bool _suppressWpfOutput;
        bool _enableLoggingTrace;
        string _logFilePath = DefaultLogFilePath;

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting", GroupName = OpGroupName)]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetValue(ref _enableBroadcasting, value);
        }

        [XmlElement("EnableAdminsOnly")]
        [Display(Order = 1, Name = "Broadcast to admins only", GroupName = OpGroupName)]
        public bool AdminsOnly
        {
            get => _adminsOnly;
            set => SetValue(ref _adminsOnly, value);
        }

        [XmlElement("FirstIdleSeconds")]
        [Display(Order = 2, Name = "First idle seconds", GroupName = OpGroupName)]
        public double FirstIdleSeconds
        {
            get => _firstIdleSeconds;
            set => SetValue(ref _firstIdleSeconds, value);
        }

        [XmlElement("ThresholdMspf")]
        [Display(Order = 3, Name = "Threshold ms/f per grid", GroupName = OpGroupName)]
        public float ThresholdMspf
        {
            get => _mspfThreshold;
            set => SetValue(ref _mspfThreshold, Math.Max(value, 0.001f));
        }

        [XmlElement("SimSpeedThreshold")]
        [Display(Order = 4, Name = "Threshold sim speed", GroupName = OpGroupName)]
        public double SimSpeedThreshold
        {
            get => _simSpeedThreshold;
            set => SetValue(ref _simSpeedThreshold, MathUtils.Clamp(value, 0d, 2d));
        }

        [XmlElement("MaxLaggyGridCountPerScan")]
        [Display(Order = 5, Name = "Max GPS count", GroupName = OpGroupName)]
        public int MaxReportSizePerScan
        {
            get => _maxLaggyGridCountPerScan;
            set => SetValue(ref _maxLaggyGridCountPerScan, value);
        }

        [XmlElement("BufferSeconds")]
        [Display(Order = 6, Name = "Window time (seconds)", GroupName = OpGroupName)]
        public double BufferSeconds
        {
            get => _bufferSeconds;
            set => SetValue(ref _bufferSeconds, value);
        }

        [XmlElement("GpsNameFormat")]
        [Display(Order = 7, Name = "GPS name format", GroupName = OpGroupName)]
        public string GpsNameFormat
        {
            get => _gpsNameFormat;
            set => SetValue(ref _gpsNameFormat, value);
        }

        [XmlElement("GpsDescriptionFormat")]
        [Display(Order = 8, Name = "GPS description format", GroupName = OpGroupName)]
        public string GpsDescriptionFormat
        {
            get => _gpsDescriptionFormat;
            set => SetValue(ref _gpsDescriptionFormat, value);
        }

        [XmlElement("ExemptNpcFactions")]
        [Display(Order = 9, Name = "Exempt NPC factions", GroupName = OpGroupName)]
        public bool ReportNpcFactions
        {
            get => _exemptNpcFactions;
            set => SetValue(ref _exemptNpcFactions, value);
        }

        [XmlElement("ExemptFactionTags")]
        [Display(Order = 10, Name = "Exempt faction tags", GroupName = OpGroupName)]
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

        [XmlElement("LogFilePath")]
        [Display(Order = 14, Name = "Log File Path", GroupName = LogGroupName)]
        public string LogFilePath
        {
            get => _logFilePath;
            set => SetValue(ref _logFilePath, value);
        }

        TimeSpan LaggyGridScanner.IConfig.WindowTime => BufferSeconds.Seconds();
        IEnumerable<ulong> TargetPlayerCollector.IConfig.MutedPlayers => _mutedPlayerIds;
        IEnumerable<string> GridReporter.IConfig.ExemptFactionTags => _exemptFactionTags;

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
    }
}