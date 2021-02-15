using System;

namespace AutoModerator.Punishes.Broadcasts
{
    public readonly struct GridGpsSource
    {
        public GridGpsSource(long gridId, double longLagNormal, TimeSpan remainingTime, int rank)
        {
            GridId = gridId;
            LongLagNormal = longLagNormal;
            RemainingTime = remainingTime;
            Rank = rank;
        }

        public readonly long GridId;
        public readonly double LongLagNormal;
        public readonly TimeSpan RemainingTime;
        public readonly int Rank;
    }
}