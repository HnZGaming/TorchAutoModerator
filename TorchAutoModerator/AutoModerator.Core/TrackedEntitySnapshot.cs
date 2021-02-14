using System;
using System.Collections.Generic;

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
        public bool IsPinned => RemainingTime > TimeSpan.Zero;

        // don't include temporary stuff like entity names and faction IDs
        // because those things can change
        
        public readonly struct Comparer : IComparer<TrackedEntitySnapshot>
        {
            public static readonly Comparer Instance = new Comparer();

            public int Compare(TrackedEntitySnapshot x, TrackedEntitySnapshot y)
            {
                return y.LongLagNormal.CompareTo(x.LongLagNormal); // descending
            }
        }
    }
}