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
        public TrackedEntitySnapshot(long id, string name, long ownerId, string ownerName, string factionTag, double longLagNormal, TimeSpan remainingTime, bool isBlessed)
        {
            Id = id;
            Name = name;
            OwnerId = ownerId;
            OwnerName = ownerName;
            FactionTag = factionTag;
            LongLagNormal = longLagNormal;
            RemainingTime = remainingTime;
            IsBlessed = isBlessed;
            IsPinned = remainingTime > TimeSpan.Zero;
        }

        public readonly long Id;
        public readonly string Name;
        public readonly long OwnerId;
        public readonly string OwnerName;
        public readonly string FactionTag;
        public readonly double LongLagNormal;
        public readonly TimeSpan RemainingTime;
        public readonly bool IsBlessed;
        public readonly bool IsPinned;

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