using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Utils.General;

namespace AutoModerator.Core
{
    // smart base class is an anti pattern >;(
    // careful modifying stuff in this class
    public abstract class LaggyEntityTracker<S> where S : IEntityLagSnapshot
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityLagTimeSeries _lagTimeSeries;
        readonly LifespanDictionary<long> _pinnedIds;
        readonly Dictionary<long, S> _snapshots;

        protected LaggyEntityTracker()
        {
            _lagTimeSeries = new EntityLagTimeSeries();
            _pinnedIds = new LifespanDictionary<long>();
            _snapshots = new Dictionary<long, S>();
        }

        protected abstract TimeSpan PinWindow { get; }
        protected abstract TimeSpan PinLifespan { get; }
        protected abstract bool IsFactionExempt(string factionTag);

        public int LastLaggyEntityCount { get; private set; }
        public int PinnedEntityCount => _pinnedIds.Count;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTimeSeries.Clear();
            _pinnedIds.Clear();
            _snapshots.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void Update(IEnumerable<S> snapshots)
        {
            // skip exempt factions
            snapshots = snapshots.WhereNot(s =>
                s.FactionTagOrNull is string factionTag &&
                IsFactionExempt(factionTag));

            _lagTimeSeries.AddInterval(snapshots.Select(r => (r.EntityId, r.LagNormal)));

            // map grid id -> last profile result
            _snapshots.AddRangeWithKeys(snapshots, r => r.EntityId);

            // expire old data
            var trackedGridIds = _lagTimeSeries.EntityIds.Concat(_pinnedIds.Keys);
            _snapshots.RemoveRangeExceptWith(trackedGridIds);

            // keep track of laggy grids & gps lifespan
            var laggyGridIds = _lagTimeSeries.GetLaggyEntityIds().ToArray();
            _pinnedIds.AddOrUpdate(laggyGridIds);

            // clean up old data
            _pinnedIds.Lifespan = PinLifespan;
            _pinnedIds.RemoveExpired();
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - PinWindow);

            LastLaggyEntityCount = snapshots.Count(s => s.LagNormal >= 1f);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected IEnumerable<(S Snapshot, TimeSpan RemainingTime)> GetTopPins(int count)
        {
            _pinnedIds.Lifespan = PinLifespan;

            return _pinnedIds
                .GetRemainingTimes()
                .Select(p => (Snapshot: _snapshots[p.Key], p.RemainingTime))
                .OrderByDescending(p => p.Snapshot.LagNormal)
                .Take(count);
        }
    }
}