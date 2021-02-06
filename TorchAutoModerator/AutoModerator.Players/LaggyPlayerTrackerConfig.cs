using System;
using AutoModerator.Core;
using Utils.General;

namespace AutoModerator.Players
{
    public sealed class LaggyPlayerTrackerConfig : LaggyEntityTracker.IConfig
    {
        readonly AutoModeratorConfig _masterConfig;

        public LaggyPlayerTrackerConfig(AutoModeratorConfig masterConfig)
        {
            _masterConfig = masterConfig;
        }

        public TimeSpan PinWindow => _masterConfig.PlayerPinWindow.Seconds();
        public TimeSpan PinLifespan => _masterConfig.PlayerPinLifespan.Seconds();
        public bool IsFactionExempt(string factionTag) => _masterConfig.IsFactionExempt(factionTag);
    }
}