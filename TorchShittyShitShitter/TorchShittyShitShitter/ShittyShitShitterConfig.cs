using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Torch;
using Torch.Views;
using TorchShittyShitShitter.Core;
using Utils.General;

namespace TorchShittyShitShitter
{
    public sealed class ShittyShitShitterConfig :
        ViewModel,
        LaggyGridReportBuffer.IConfig,
        LaggyGridScanner.IConfig,
        GpsBroadcaster.IConfig
    {
        double _firstIdleSeconds = 120;
        bool _enableBroadcasting = true;
        double _bufferSeconds = 60d;
        int _maxLaggyGridCountPerScan = 5;
        double _gpsLifespanSeconds = 60d;
        double _mspfPerFactionMemberLimit = 0.3d;
        List<ulong> _mutedPlayerIds;

        public ShittyShitShitterConfig()
        {
            _mutedPlayerIds = new List<ulong>();
        }

        [XmlElement("FirstIdleSeconds")]
        [Display(Order = -1, Name = "First idle seconds", Description = "All grids tend to be laggy during the first couple minutes of a session.")]
        public double FirstIdleSeconds
        {
            get => _firstIdleSeconds;
            set => SetProperty(ref _firstIdleSeconds, value);
        }

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting")]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetProperty(ref _enableBroadcasting, value);
        }

        [XmlElement("MspfPerFactionMemberLimit")]
        [Display(Order = 1, Name = "Limit ms/f per online member", Description = "\"Lagginess\" is calculated by a faction's sim impact divided by its online member count.")]
        public double MspfPerFactionMemberLimit
        {
            get => _mspfPerFactionMemberLimit;
            set => SetProperty(ref _mspfPerFactionMemberLimit, value);
        }

        [XmlElement("MaxLaggyGridCountPerScan")]
        [Display(Order = 2, Name = "Max laggy grid count per scan", Description = "Too many GPS entities can abstract the general sight of players and cause a server sim drop.")]
        public int MaxLaggyGridCountPerScan
        {
            get => _maxLaggyGridCountPerScan;
            set => SetProperty(ref _maxLaggyGridCountPerScan, value);
        }

        [XmlElement("BufferSeconds")]
        [Display(Order = 3, Name = "Buffer seconds", Description = "Factions that are laggy for a length of time will be broadcast.")]
        public double BufferSeconds
        {
            get => _bufferSeconds;
            set => SetProperty(ref _bufferSeconds, value);
        }

        [XmlElement("GpsLifespanSeconds")]
        [Display(Order = 4, Name = "GPS lifespan seconds", Description = "Top grid's GPS will stay active for a length of time even if its faction is no longer laggy.")]
        public double GpsLifespanSeconds
        {
            get => _gpsLifespanSeconds;
            set => SetProperty(ref _gpsLifespanSeconds, value);
        }

        [XmlElement("MutedPlayerIds")]
        [Display(Order = 5, Name = "Muted Players", Description = "Players can mute GPS broadcaster.")]
        [Obsolete("For UI and serialization only; use Add/Remove methods to modify this list.")]
        public List<ulong> MutedPlayerIds
        {
            get => _mutedPlayerIds;
            set => SetProperty(ref _mutedPlayerIds, new HashSet<ulong>(value).ToList());
        }

        TimeSpan LaggyGridReportBuffer.IConfig.WindowTime => BufferSeconds.Seconds();
        TimeSpan GpsBroadcaster.IConfig.GpsLifespan => _gpsLifespanSeconds.Seconds();
        IEnumerable<ulong> GpsBroadcaster.IConfig.MutedPlayers => _mutedPlayerIds;

        public void AddMutedPlayer(ulong mutedPlayerId)
        {
            if (!_mutedPlayerIds.Contains(mutedPlayerId))
            {
                _mutedPlayerIds.Add(mutedPlayerId);
                OnPropertyChanged();
            }
        }

        public void RemoveMutedPlayer(ulong unmutedPlayerId)
        {
            if (_mutedPlayerIds.Remove(unmutedPlayerId))
            {
                OnPropertyChanged();
            }
        }

        void SetProperty<T>(ref T property, T value)
        {
            if (!property.Equals(value))
            {
                property = value;
                OnPropertyChanged();
            }
        }
    }
}