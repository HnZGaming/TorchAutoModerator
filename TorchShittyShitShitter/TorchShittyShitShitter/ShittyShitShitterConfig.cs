using System;
using System.Xml.Serialization;
using Torch;
using Torch.Views;
using TorchShittyShitShitter.Core;
using Utils.General;

namespace TorchShittyShitShitter
{
    public sealed class ShittyShitShitterConfig :
        ViewModel,
        LaggyGridWindowBuffer.IConfig,
        LaggyGridCollector.IConfig,
        LaggyGridGpsBroadcaster.IConfig
    {
        bool _enableBroadcasting = true;
        double _bufferSeconds = 60d;
        double _gpsLifespanSeconds = 60d;
        double _mspfPerFactionMemberLimit = 0.3d;

        [XmlElement("EnableBroadcasting")]
        [Display(Order = 0, Name = "Enable broadcasting")]
        public bool EnableBroadcasting
        {
            get => _enableBroadcasting;
            set => SetProperty(ref _enableBroadcasting, value);
        }

        [XmlElement("MspfPerFactionMemberLimit")]
        [Display(Order = 1, Name = "Limit ms/f per online member", Description = "Lagginess is calculated by a faction's sim impact divided by its online member count.")]
        public double MspfPerFactionMemberLimit
        {
            get => _mspfPerFactionMemberLimit;
            set => SetProperty(ref _mspfPerFactionMemberLimit, Math.Max(0.1d, value));
        }

        [XmlElement("BufferSeconds")]
        [Display(Order = 2, Name = "Buffer seconds", Description = "Factions that are laggy for a length of time will be broadcast.")]
        public double BufferSeconds
        {
            get => _bufferSeconds;
            set => SetProperty(ref _bufferSeconds, value);
        }

        [XmlElement("GpsLifespanSeconds")]
        [Display(Order = 3, Name = "GPS lifespan seconds", Description = "Top grid's GPS will stay active for a length of time even if its faction is no longer laggy.")]
        public double GpsLifespanSeconds
        {
            get => _gpsLifespanSeconds;
            set => SetProperty(ref _gpsLifespanSeconds, value);
        }

        void SetProperty<T>(ref T property, T value)
        {
            if (!property.Equals(value))
            {
                property = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan WindowTime => BufferSeconds.Seconds();
        public TimeSpan GpsLifespan => _gpsLifespanSeconds.Seconds();
    }
}