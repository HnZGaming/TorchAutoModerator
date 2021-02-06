using System;

namespace AutoModerator.Core
{
    public readonly struct TrackedEntitySnapshot
    {
        public TrackedEntitySnapshot(long entityId, double longLagNormal, TimeSpan remainingTime)
        {
            EntityId = entityId;
            LongLagNormal = longLagNormal;
            RemainingTime = remainingTime;
        }

        public long EntityId { get; }
        public double LongLagNormal { get; }
        public TimeSpan RemainingTime { get; }
    }
}