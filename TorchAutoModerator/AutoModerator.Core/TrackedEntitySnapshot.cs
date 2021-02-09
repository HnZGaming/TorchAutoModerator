using System;

namespace AutoModerator.Core
{
    public readonly struct TrackedEntitySnapshot
    {
        public TrackedEntitySnapshot(long id, double longLagNormal, TimeSpan remainingTime)
        {
            Id = id;
            LongLagNormal = longLagNormal;
            RemainingTime = remainingTime;
        }

        public long Id { get; }
        public double LongLagNormal { get; }
        public TimeSpan RemainingTime { get; }
    }
}