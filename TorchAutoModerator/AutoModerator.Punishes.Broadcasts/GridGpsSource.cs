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

        public long GridId { get; }
        public double LongLagNormal { get; }
        public TimeSpan RemainingTime { get; }
        public int Rank { get; }
    }
}