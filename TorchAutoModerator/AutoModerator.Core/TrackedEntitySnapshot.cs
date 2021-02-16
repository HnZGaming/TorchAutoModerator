using System;
using System.Collections.Generic;

namespace AutoModerator.Core
{
    /// <summary>
    /// Snapshot of a tracked entity.
    /// </summary>
    /// <remarks>
    /// "Snapshot" means the state at one moment in the past and
    /// it's potentially changed by the time it's consumed.
    /// </remarks>
    public readonly struct TrackedEntitySnapshot
    {
        public TrackedEntitySnapshot(long id, string name, long ownerId, string ownerName, double longLagNormal, TimeSpan remainingTime)
        {
            Id = id;
            Name = name;
            OwnerId = ownerId;
            OwnerName = ownerName;
            LongLagNormal = longLagNormal;
            RemainingTime = remainingTime;
        }

        public readonly long Id;
        public readonly string Name;
        public readonly long OwnerId;
        public readonly string OwnerName;
        public readonly double LongLagNormal;
        public readonly TimeSpan RemainingTime;
        public bool IsPinned => RemainingTime > TimeSpan.Zero;

        public readonly struct LongLagComparer : IComparer<TrackedEntitySnapshot>
        {
            public static readonly LongLagComparer Instance = new LongLagComparer();

            public int Compare(TrackedEntitySnapshot x, TrackedEntitySnapshot y)
            {
                return y.LongLagNormal.CompareTo(x.LongLagNormal); // descending
            }
        }
    }
}