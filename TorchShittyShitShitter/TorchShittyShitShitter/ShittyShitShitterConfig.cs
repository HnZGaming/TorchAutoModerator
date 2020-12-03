using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Torch;
using Torch.Views;
using TorchShittyShitShitter.Core;
using TorchShittyShitShitter.Core.Scanners;
using Utils.General;

namespace TorchShittyShitShitter
{
    public sealed class ShittyShitShitterConfig :
        ViewModel,
        LaggyGridReportBuffer.IConfig,
        LaggyGridFinder.IConfig,
        ILagScannerConfig,
        GpsBroadcaster.IConfig,
        ServerLagObserver.IConfig
    {
        double _firstIdleSeconds = 120;
        bool _enableBroadcasting = true;
        bool _enableAdminsOnly = true;
        double _bufferSeconds = 60d;
        int _maxLaggyGridCountPerScan = 3;
        double _gpsLifespanSeconds = 60d;
        double _mspfPerFactionMemberLimit = 0.3d;
        double _simSpeedThreshold = 0.7;
        List<ulong> _mutedPlayerIds;

        public ShittyShitShitterConfig()
        {
            _mutedPlayerIds = new List<ulong>();
        }

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting", Description = "Tick off to stop broadcasting new GPS entities.")]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetValue(ref _enableBroadcasting, value);
        }

        [XmlElement("EnableAdminsOnly")]
        [Display(Order = 1, Name = "Broadcast to admins only", Description = "Broadcast to admin players only.")]
        public bool EnableAdminsOnly
        {
            get => _enableAdminsOnly;
            set => SetValue(ref _enableAdminsOnly, value);
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
            set => SetValue(ref _mspfPerFactionMemberLimit, value);
        }

        [XmlElement("MaxLaggyGridCountPerScan")]
        [Display(Order = 4, Name = "Max GPS count", Description = "Too many GPS entities can cause issues: block the sight of players and drop the server sim.")]
        public int MaxReportCountPerScan
        {
            get => _maxLaggyGridCountPerScan;
            set => SetValue(ref _maxLaggyGridCountPerScan, value);
        }

        [XmlElement("BufferSeconds")]
        [Display(Order = 5, Name = "Window time (seconds)", Description = "Factions that are laggy for a length of time will be broadcast.")]
        public double BufferSeconds
        {
            get => _bufferSeconds;
            set => SetValue(ref _bufferSeconds, value);
        }

        [XmlElement("GpsLifespanSeconds")]
        [Display(Order = 6, Name = "GPS lifespan (seconds)", Description = "Top grid's GPS will stay active for a length of time even if its faction is no longer laggy.")]
        public double GpsLifespanSeconds
        {
            get => _gpsLifespanSeconds;
            set => SetValue(ref _gpsLifespanSeconds, value);
        }

        [XmlElement("SimSpeedThreshold")]
        [Display(Order = 7, Name = "Threshold sim speed", Description = "Broadcast begins when the server sim speed drops.")]
        public double SimSpeedThreshold
        {
            get => _simSpeedThreshold;
            set => SetValue(ref _simSpeedThreshold, MathUtils.Clamp(value, 0, 2));
        }

        [XmlElement("MutedPlayerIds")]
        [Display(Order = 8, Name = "Muted Players", Description = "Players can mute GPS broadcaster with a command.")]
        public List<ulong> MutedPlayerIds
        {
            get => _mutedPlayerIds;
            set => SetValue(ref _mutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        TimeSpan LaggyGridReportBuffer.IConfig.WindowTime => BufferSeconds.Seconds();
        TimeSpan GpsBroadcaster.IConfig.GpsLifespan => _gpsLifespanSeconds.Seconds();
        IEnumerable<ulong> GpsBroadcaster.IConfig.MutedPlayers => _mutedPlayerIds;

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