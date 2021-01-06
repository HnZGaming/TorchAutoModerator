using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using AutoModerator.Core;
using AutoModerator.Core.Scanners;
using Torch;
using Torch.Views;
using Utils.General;

namespace AutoModerator
{
    public sealed class AutoModeratorConfig :
        ViewModel,
        LaggyGridReportBuffer.IConfig,
        LaggyGridFinder.IConfig,
        ILagScannerConfig,
        LaggyGridGpsBroadcaster.IConfig,
        ServerLagObserver.IConfig,
        LaggyGridGpsDescriptionMaker.IConfig
    {
        double _firstIdleSeconds = 180;
        bool _enableBroadcasting = true;
        bool _adminsOnly = true;
        int _maxLaggyGridCountPerScan = 3;
        double _bufferSeconds = 300d;
        double _gpsLifespanSeconds = 600d;
        double _mspfPerFactionMemberLimit = 3.0d;
        double _simSpeedThreshold = 0.7;
        bool _exemptNpcFactions = true;
        string _gpsDescriptionFormat = "The {rank} laggiest faction ({ratio}). Get 'em!";
        List<ulong> _mutedPlayerIds = new List<ulong>();
        List<string> _exemptFactionTags = new List<string>();

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting", Description = "Tick off to stop broadcasting new GPS entities.")]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetValue(ref _enableBroadcasting, value);
        }

        [XmlElement("EnableAdminsOnly")]
        [Display(Order = 1, Name = "Broadcast to admins only", Description = "Broadcast to admin players only.")]
        public bool AdminsOnly
        {
            get => _adminsOnly;
            set => SetValue(ref _adminsOnly, value);
        }

        [XmlElement("FirstIdleSeconds")]
        [Display(Order = 2, Name = "First idle seconds", Description = "All grids tend to be laggy during the first couple minutes of a session.")]
        public double FirstIdleSeconds
        {
            get => _firstIdleSeconds;
            set => SetValue(ref _firstIdleSeconds, value);
        }

        [XmlElement("MspfPerFactionMemberLimit")]
        [Display(Order = 3, Name = "Threshold ms/f per online member", Description = "\"Lagginess\" is calculated by a faction's sim impact divided by its online member count.")]
        public double MspfPerOnlineGroupMember
        {
            get => _mspfPerFactionMemberLimit;
            set => SetValue(ref _mspfPerFactionMemberLimit, Math.Max(value, 0.001d));
        }

        [XmlElement("SimSpeedThreshold")]
        [Display(Order = 4, Name = "Threshold sim speed", Description = "Broadcast begins when the server sim speed drops.")]
        public double SimSpeedThreshold
        {
            get => _simSpeedThreshold;
            set => SetValue(ref _simSpeedThreshold, MathUtils.Clamp(value, 0d, 2d));
        }

        [XmlElement("MaxLaggyGridCountPerScan")]
        [Display(Order = 5, Name = "Max GPS count", Description = "Too many GPS entities can cause issues: block the sight of players and drop the server sim.")]
        public int MaxReportCountPerScan
        {
            get => _maxLaggyGridCountPerScan;
            set => SetValue(ref _maxLaggyGridCountPerScan, value);
        }

        [XmlElement("BufferSeconds")]
        [Display(Order = 6, Name = "Window time (seconds)", Description = "Factions that are laggy for a length of time will be broadcast.")]
        public double BufferSeconds
        {
            get => _bufferSeconds;
            set => SetValue(ref _bufferSeconds, value);
        }

        [XmlElement("GpsLifespanSeconds")]
        [Display(Order = 7, Name = "GPS lifespan (seconds)", Description = "Top grid's GPS will stay active for a length of time even if its faction is no longer laggy.")]
        public double GpsLifespanSeconds
        {
            get => _gpsLifespanSeconds;
            set => SetValue(ref _gpsLifespanSeconds, value);
        }

        [XmlElement("ExemptNpcFactions")]
        [Display(Order = 8, Name = "Exempt NPC factions", Description = "Ignore NPC factions in scan results.")]
        public bool ExemptNpcFactions
        {
            get => _exemptNpcFactions;
            set => SetValue(ref _exemptNpcFactions, value);
        }

        [XmlElement("GpsDescriptionFormat")]
        [Display(Order = 8, Name = "GPS description format", Description = "{rank} -- rank; ex: \"1st\", \"2nd\". {ratio} -- ratio to ms/f threshold; ex: \"121%\".")]
        public string GpsDescriptionFormat
        {
            get => _gpsDescriptionFormat;
            set => SetValue(ref _gpsDescriptionFormat, value);
        }

        [XmlElement("ExemptFactionTags")]
        [Display(Order = 10, Name = "Exempt faction tags", Description = "Tags of factions that will not be broadcasted.")]
        public List<string> ExemptFactionTags
        {
            get => _exemptFactionTags;
            set => SetValue(ref _exemptFactionTags, new HashSet<string>(value).ToList());
        }

        [XmlElement("MutedPlayerIds")]
        [Display(Order = 11, Name = "Muted players", Description = "Players can mute GPS broadcaster with a command.")]
        public List<ulong> MutedPlayerIds
        {
            get => _mutedPlayerIds;
            set => SetValue(ref _mutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        TimeSpan LaggyGridReportBuffer.IConfig.WindowTime => BufferSeconds.Seconds();
        TimeSpan LaggyGridGpsBroadcaster.IConfig.GpsLifespan => _gpsLifespanSeconds.Seconds();
        IEnumerable<ulong> LaggyGridGpsBroadcaster.IConfig.MutedPlayers => _mutedPlayerIds;
        IEnumerable<string> LaggyGridFinder.IConfig.ExemptFactionTags => _exemptFactionTags;

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