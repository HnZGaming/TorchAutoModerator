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
            double PinLag { get; }
            double OutlierFenceNormal { get; }
            TimeSpan TrackingSpan { get; }
            TimeSpan PinSpan { get; }
            bool IsFactionExempt(long factionId);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly TaggedTimeSeries<long, double> _lagTimeSeries;
        readonly ExpiryDictionary<long> _pinnedIds;
        readonly Dictionary<long, EntityLagSource> _lastSources;
        readonly Dictionary<long, TrackedEntitySnapshot> _lastSnapshots;

        public EntityLagTracker(IConfig config)
        {
            _config = config;
            _lagTimeSeries = new TaggedTimeSeries<long, double>();
            _pinnedIds = new ExpiryDictionary<long>();
            _lastSources = new Dictionary<long, EntityLagSource>();
            _lastSnapshots = new Dictionary<long, TrackedEntitySnapshot>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetTrackedEntity(long entityId, out TrackedEntitySnapshot entity)
        {
            return _lastSnapshots.TryGetValue(entityId, out entity);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<long> GetTrackedEntityIds()
        {
            return _lastSnapshots.Keys.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities()
        {
            return _lastSnapshots.Values.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTopPins()
        {
            return _lastSnapshots
                .Values
                .Where(s => s.IsPinned)
                .OrderByDescending(s => s.LongLagNormal)
                .ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetTimeSeries(long entityId, out ITimeSeries<double> timeSeries)
        {
            return _lagTimeSeries.TryGetTimeSeries(entityId, out timeSeries);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTimeSeries.Clear();
            _pinnedIds.Clear();
            _lastSources.Clear();
            _lastSnapshots.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StopTracking(long entityId)
        {
            _lagTimeSeries.RemoveSeries(entityId);
            _pinnedIds.Remove(entityId);
            _lastSources.Remove(entityId);
            _lastSnapshots.Remove(entityId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<EntityLagSource> sources)
        {
            // clean up old data
            var now = DateTime.UtcNow;
            _lagTimeSeries.RemovePointsOlderThan(now - _config.TrackingSpan);
            _pinnedIds.RemoveExpired();

            // stop tracking non-existing (deleted) entities
            _lagTimeSeries.RemoveWhere(s => s.All(p => p.Element == 0d));

            // find valid entities
            var validSources = new Dictionary<long, EntityLagSource>();
            var foundInvalidValues = false;
            foreach (var src in sources)
            {
                // skip exempt faction's entities
                if (_config.IsFactionExempt(src.FactionId))
                {
                    Log.Trace($"exempt: {src}");
                    continue;
                }

                if (!src.LagMspf.IsValid())
                {
                    foundInvalidValues = true;
                    continue;
                }

                validSources.Add(src.EntityId, src);
                _lastSources[src.EntityId] = src;
            }

            if (foundInvalidValues)
            {
                Log.Warn("invalid ms/f value(s) found; maybe: server freezing");
            }

            // track entities
            foreach (var src in validSources.Values)
            {
                var lagNormal = src.LagMspf / _config.PinLag;
                _lagTimeSeries.AddPoint(src.EntityId, now, lagNormal);

                Log.Trace($"input: {src} -> {lagNormal * 100:0}%");
            }

            // update all tracked entities' interval with zero value if didn't get a new input
            foreach (var existingEntityId in _lagTimeSeries.Tags)
            {
                if (!validSources.ContainsKey(existingEntityId))
                {
                    _lagTimeSeries.AddPoint(existingEntityId, now, 0);
                }
            }

            // analyze long lags
            var longLags = new Dictionary<long, double>();
            foreach (var (entityId, timeSeries) in _lagTimeSeries.GetAllTimeSeries())
            {
                var longLag = CalcLongLagNormal(timeSeries);
                longLags.Add(entityId, longLag);

                // don't pin until sufficient data is available (even if long-laggy)
                // but need to get players warned in case this entity just got spawned
                var minTimestamp = DateTime.UtcNow - (_config.TrackingSpan - 5.Seconds());
                var notEnoughData = timeSeries.IsAllYoungerThan(minTimestamp);

                // pin long-laggy entities
                if (longLag >= 1 && !notEnoughData)
                {
                    _pinnedIds.AddOrUpdate(entityId, _config.PinSpan);
                }
            }

            // take snapshots
            _lastSnapshots.Clear();
            foreach (var (entityId, (longLag, pin)) in longLags.Zip(_pinnedIds.ToDictionary()))
            {
                var lastSource = _lastSources.GetValueOrDefault(entityId);
                var name = lastSource.Name ?? $"<{entityId}>";
                var ownerId = lastSource.OwnerId;
                var ownerName = lastSource.OwnerName ?? $"<{ownerId}>";
                var snapshot = new TrackedEntitySnapshot(entityId, name, ownerId, ownerName, longLag, pin);
                _lastSnapshots.Add(entityId, snapshot);
            }

            if (Log.IsDebugEnabled)
            {
                var allTrackedEntities = GetTrackedEntities().ToArray(); // including pins
                if (allTrackedEntities.Any())
                {
                    foreach (var entity in allTrackedEntities)
                    {
                        var name = _lastSources.GetValueOrDefault(entity.Id).Name ?? $"<{entity.Id}>";
                        var currentMspf = validSources.TryGetValue(entity.Id, out var s) ? $"{s.LagMspf:0.00}ms/f" : "--ms/f";
                        var tsCount = _lagTimeSeries.TryGetTimeSeries(entity.Id, out var ts) ? ts.Count : 0;
                        var pinSecs = entity.RemainingTime.TotalSeconds;
                        var pin = pinSecs > 0 ? $"pin: {pinSecs:0}secs" : "not pinned";
                        Log.Debug($"tracking: \"{name}\" ({entity.Id}) -> {currentMspf} {entity.LongLagNormal * 100:0}% ({tsCount}) {pin}");
                    }
                }
                else
                {
                    Log.Debug("tracking 0 entities");
                }
            }
        }

        // returned value of 1+ will (generally) pin given entity for punishment
        double CalcLongLagNormal(ITimeSeries<double> timeSeries)
        {
            if (timeSeries.Count == 0) return 0;
            if (timeSeries.Count == 1) return timeSeries[0].Element; // for outlier test

            var totalNormal = 0d;
            var outlierTests = timeSeries.TestOutlier();
            for (var i = 0; i < timeSeries.Count; i++)
            {
                var (timestamp, normal) = timeSeries[i];
                var outlierTest = outlierTests[i];

                // first interval is most always laggy due to spawning or server hiccup
                // NOTE the element at index 0 is not necessarily the very first element
                // because we constantly delete old elements every update
                if (i == 0)
                {
                    totalNormal += Math.Min(1, normal);
                    continue;
                }

                if (_config.OutlierFenceNormal > 0) // switch
                {
                    // prevent catching server hiccups
                    if (outlierTest > _config.OutlierFenceNormal)
                    {
                        totalNormal += Math.Min(1, normal);
                        continue;
                    }
                }

                totalNormal += normal;
            }

            var avgNormal = totalNormal / timeSeries.Count;
            return avgNormal;
        }
    }
}