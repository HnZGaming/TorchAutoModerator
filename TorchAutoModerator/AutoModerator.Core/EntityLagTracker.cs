using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MathNet.Numerics.Statistics;
using NLog;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Core
{
    public sealed class EntityLagTracker
    {
        public interface IConfig
        {
            double PinLag { get; }
            double OutlierFenceNormal { get; }
            TimeSpan TrackingSpan { get; }
            TimeSpan PinSpan { get; }
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
        public void Update(IEnumerable<EntityLagSource> sources)
        {
            // skip exempt factions
            sources = sources
                .WhereNot(s =>
                    s.FactionTag is string factionTag &&
                    _config.IsFactionExempt(factionTag))
                .ToArray();

            Log.Debug($"{sources.Count(s => s.LagMspf >= 1f)} laggy entities profiled in this interval");

            var sourceMap = sources.ToDictionary(l => l.EntityId);

            // track entities
            var now = DateTime.UtcNow;
            foreach (var src in sources)
            {
                var lagNormal = src.LagMspf / _config.PinLag;
                _lagTimeSeries.AddPoint(src.EntityId, now, lagNormal);
            }

            // update all tracked entities' interval with zero value if didn't get a new input
            foreach (var existingEntityId in _lagTimeSeries.Tags)
            {
                if (!sourceMap.ContainsKey(existingEntityId))
                {
                    _lagTimeSeries.AddPoint(existingEntityId, now, 0);
                }
            }

            // remove old points
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - _config.TrackingSpan);

            // stop tracking not-laggy entities
            _lagTimeSeries.RemoveWhere(s => s.All(p => p.Element == 0d));

            // keep track of laggy entities & pin span
            var laggyEntityIds = GetLongLagNormals(1d).Select(p => p.EntityId);
            _pinnedIds.AddOrUpdate(laggyEntityIds, _config.PinSpan);

            // clean up old data
            _pinnedIds.RemoveExpired();

            if (Log.IsDebugEnabled)
            {
                foreach (var src in sources)
                {
                    _names[src.EntityId] = src.Name;
                }

                foreach (var entity in GetTrackedEntities(0))
                {
                    var name = _names.GetOrElse(entity.Id, $"{entity.Id}");
                    var latest = sourceMap.TryGetValue(entity.Id, out var e) ? $"{e.LagMspf:0.00}ms/f" : "--ms/f";
                    Log.Debug($"tracked entity: \"{name}\" -> {latest} {entity.LongLagNormal * 100:0}% {entity.RemainingTime.TotalSeconds:0}s");
                }
            }

            Log.Debug($"{_pinnedIds.Count} pinned entities");
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
            var minTimestamp = DateTime.UtcNow - _config.TrackingSpan;
            if (oldestTimestamp > minTimestamp) return 0;

            var mean = timeSeries.Elements.Mean();
            var stdev = timeSeries.Elements.StandardDeviation();

            var sumNormal = 0d;
            foreach (var (timestamp, normal) in timeSeries)
            {
                if (normal < 1)
                {
                    sumNormal += normal;
                    continue;
                }

                var test = (normal - mean) / stdev;
                if (test > _config.OutlierFenceNormal)
                {
                    sumNormal += 1; // clamp top if possibly a server hiccup
                    continue;
                }

                sumNormal += normal;
            }

            var avgNormal = sumNormal / timeSeries.Count;
            return avgNormal;
        }
    }
}