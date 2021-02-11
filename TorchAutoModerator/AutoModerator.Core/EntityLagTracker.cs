using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Core
{
    public sealed class EntityLagTracker
    {
        public interface IConfig
        {
            double LagThreshold { get; }
            int SafetyInterval { get; }
            TimeSpan PinWindow { get; }
            TimeSpan PinLifeSpan { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly TaggedTimeSeries<long, double> _lagTimeSeries;
        readonly ExpiryDictionary<long> _pinnedIds;
        readonly Dictionary<long, string> _names; // for debugging only

        public EntityLagTracker(IConfig config)
        {
            _config = config;
            _lagTimeSeries = new TaggedTimeSeries<long, double>();
            _pinnedIds = new ExpiryDictionary<long>();
            _names = new Dictionary<long, string>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<EntityLagSnapshot> entityLags)
        {
            // skip exempt factions
            entityLags = entityLags.WhereNot(s =>
                s.FactionTag is string factionTag &&
                _config.IsFactionExempt(factionTag));

            var now = DateTime.UtcNow;
            foreach (var lag in entityLags)
            {
                var lagNormal = lag.LagMspf / _config.LagThreshold;
                _lagTimeSeries.AddPoint(lag.EntityId, now, lagNormal);
            }

            // append zero to time series that didn't have new input
            var profiledEntityIdSet = entityLags.Select(r => r.EntityId).ToSet();
            foreach (var existingGridId in _lagTimeSeries.Tags)
            {
                if (!profiledEntityIdSet.Contains(existingGridId))
                {
                    _lagTimeSeries.AddPoint(existingGridId, now, 0);
                }
            }

            // keep track of laggy grids & lifespan
            var laggyGridIds = GetLongLagNormals(1d).Select(p => p.EntityId).ToArray();
            _pinnedIds.AddOrUpdate(laggyGridIds, _config.PinLifeSpan);

            // clean up old data
            _pinnedIds.RemoveExpired();
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - _config.PinWindow);

            var quietGridIds = _lagTimeSeries
                .GetAllTimeSeries()
                .Where(p => p.TimeSeries.All(t => t.Element == 0d))
                .Select(p => p.Key);

            _lagTimeSeries.RemoveSeriesRange(quietGridIds);

            // for debugging
            foreach (var lag in entityLags)
            {
                _names[lag.EntityId] = lag.Name;
            }

            if (Log.IsTraceEnabled)
            {
                foreach (var grid in GetTrackedEntities(.5d))
                {
                    var name = _names.GetOrElse(grid.Id, $"{grid.Id}");
                    Log.Trace($"entity lag: \"{name}\" -> {grid.LongLagNormal * 100:0}% {grid.RemainingTime.TotalSeconds:0}s");
                }
            }

            Log.Debug($"{entityLags.Count(s => s.LagMspf >= 1f)} laggy entities");
            Log.Debug($"{_pinnedIds.Count} pinned entities (new: {laggyGridIds.Length})");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetTrackedEntity(long entityId, out TrackedEntitySnapshot entity)
        {
            var hasPoint = TryGetLongLagNormal(entityId, out var lag);
            var hasPin = _pinnedIds.TryGetRemainingTime(entityId, out var remainingTime);
            if (!hasPoint && !hasPin)
            {
                entity = default;
                return false;
            }

            entity = new TrackedEntitySnapshot(entityId, lag, remainingTime);
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<long> GetTrackedEntityIds()
        {
            var set = new HashSet<long>();
            set.UnionWith(_lagTimeSeries.Tags);
            set.UnionWith(_pinnedIds.Keys);
            return set;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities(double minLongLagNormal)
        {
            var zip = GetLongLagNormals(minLongLagNormal)
                .ToDictionary()
                .Zip(_pinnedIds.ToDictionary());

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
                var longLagNormal = TryGetLongLagNormal(entityId, out var n) ? n : 0d;
                var snapshot = new TrackedEntitySnapshot(entityId, longLagNormal, remainingTime);
                snapshots.Add(snapshot);
            }

            return snapshots.OrderByDescending(s => s.LongLagNormal);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTimeSeries.Clear();
            _pinnedIds.Clear();
        }

        IEnumerable<(long EntityId, double Normal)> GetLongLagNormals(double minNormal)
        {
            var normals = new List<(long, double)>();
            foreach (var (entityId, timeSeries) in _lagTimeSeries.GetAllTimeSeries())
            {
                var longLagNormal = CalcLongLagNormal(timeSeries);
                if (longLagNormal > minNormal)
                {
                    normals.Add((entityId, longLagNormal));
                }
            }

            return normals;
        }

        bool TryGetLongLagNormal(long entityId, out double longLagNormal)
        {
            if (_lagTimeSeries.TryGetTimeSeries(entityId, out var timeSeries))
            {
                longLagNormal = CalcLongLagNormal(timeSeries);
                return true;
            }

            longLagNormal = 0d;
            return false;
        }

        // returning 1.0 (or higher) will make the tracker pin the entity
        double CalcLongLagNormal(ITimeSeries<double> timeSeries)
        {
            if (timeSeries.Count == 0) return 0;

            // don't evaluate until sufficient data is available
            var oldestTimestamp = timeSeries[0].Timestamp;
            var minTimestamp = DateTime.UtcNow - _config.PinWindow;
            if (oldestTimestamp > minTimestamp) return 0;

            var sumNormal = 0d;
            var sumCount = 0;
            var consecutiveLaggyFrameCount = 0;
            for (var i = 0; i < timeSeries.Count; i++)
            {
                var (timestamp, normal) = timeSeries[i];

                if (normal < 1)
                {
                    consecutiveLaggyFrameCount = 0;
                }
                else if (consecutiveLaggyFrameCount++ < _config.SafetyInterval)
                {
                    normal = 1; // flatten to 1 if it's potentially a server hiccup
                }

                sumNormal += normal;
                sumCount += 1;
            }

            var avgNormal = sumNormal / sumCount;
            return avgNormal;
        }
    }
}