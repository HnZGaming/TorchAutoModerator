using System;
using AutoModerator.Core;
using Utils.General;

namespace AutoModerator.Grids
{
    public sealed class LaggyGridTrackerConfig : LaggyEntityTracker.IConfig
    {
        readonly AutoModeratorConfig _masterConfig;

        public LaggyGridTrackerConfig(AutoModeratorConfig masterConfig)
        {
            _masterConfig = masterConfig;
        }

        public TimeSpan PinWindow => _masterConfig.GridPinWindow.Seconds();
        public TimeSpan PinLifeSpan => _masterConfig.GridPinLifespan.Seconds();
        public bool IsFactionExempt(string factionTag) => _masterConfig.IsFactionExempt(factionTag);
    }
}