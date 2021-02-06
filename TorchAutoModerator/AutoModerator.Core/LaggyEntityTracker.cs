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
    public sealed class LaggyEntityTracker
    {
        public interface IConfig
        {
            TimeSpan PinWindow { get; }
            TimeSpan PinLifespan { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly EntityLagTimeSeries _lagTimeSeries;
        readonly LifespanDictionary<long> _pinnedIds;

        public LaggyEntityTracker(IConfig config)
        {
            _config = config;
            _lagTimeSeries = new EntityLagTimeSeries();
            _pinnedIds = new LifespanDictionary<long>();
        }

        public IEnumerable<long> GetTrackedEntityIds()
        {
            var set = new HashSet<long>();
            set.UnionWith(_lagTimeSeries.EntityIds);
            set.UnionWith(_pinnedIds.Keys);
            return set;
        }

        public double GetLongLagNormal(long entityId)
        {
            return _lagTimeSeries.GetLongLagNormal(entityId);
        }

        public bool IsPinned(long entityId)
        {
            return _pinnedIds.ContainsKey(entityId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTimeSeries.Clear();
            _pinnedIds.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<IEntityLagSnapshot> snapshots)
        {
            // clean up old data
            _pinnedIds.Lifespan = _config.PinLifespan;
            _pinnedIds.RemoveExpired();
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - _config.PinWindow);

            // skip exempt factions
            snapshots = snapshots.WhereNot(s =>
                s.FactionTagOrNull is string factionTag &&
                _config.IsFactionExempt(factionTag));

            _lagTimeSeries.AddInterval(snapshots.Select(r => (r.EntityId, r.LagNormal)));

            // keep track of laggy grids & gps lifespan
            var laggyGridIds = _lagTimeSeries.GetLongLagNormals(1).Select(p => p.EntityId);
            _pinnedIds.AddOrUpdate(laggyGridIds);

            Log.Debug($"{snapshots.Count(s => s.LagNormal >= 1f)} laggy entities");
            Log.Debug($"{_pinnedIds.Count} pinned entities");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntitySnapshots(double minLongLagNormal)
        {
            _pinnedIds.Lifespan = _config.PinLifespan;

            var zip = _lagTimeSeries
                .GetLongLagNormalDictionary(minLongLagNormal)
                .Zip(_pinnedIds.ToDictionary(), 0, TimeSpan.Zero);

            var snapshots = new List<TrackedEntitySnapshot>();
            foreach (var (entityId, (longLagNormal, remainingTime)) in zip)
            {
                var snapshot = new TrackedEntitySnapshot(entityId, longLagNormal, remainingTime);
                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTopPins()
        {
            var snapshots = new List<TrackedEntitySnapshot>();
            foreach (var (entityId, remainingTime) in _pinnedIds.GetRemainingTimes())
            {
                var lagNormal = _lagTimeSeries.GetLongLagNormal(entityId);
                var snapshot = new TrackedEntitySnapshot(entityId, lagNormal, remainingTime);
                snapshots.Add(snapshot);
            }

            return snapshots.OrderByDescending(s => s.LongLagNormal);
        }
    }
}