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
            TimeSpan GracePeriodSpan { get; }
            bool IsFactionExempt(long factionId);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly TaggedTimeSeries<long, double> _lagTimeSeries;
        readonly ExpiryDictionary<long> _pinnedIds;
        readonly Dictionary<long, EntityLagSource> _lastSources;
        readonly Dictionary<long, TrackedEntitySnapshot> _lastSnapshots;
        readonly Dictionary<long, DateTime> _firstTrackedTimestamps;

        public EntityLagTracker(IConfig config)
        {
            _config = config;
            _lagTimeSeries = new TaggedTimeSeries<long, double>();
            _pinnedIds = new ExpiryDictionary<long>();
            _lastSources = new Dictionary<long, EntityLagSource>();
            _lastSnapshots = new Dictionary<long, TrackedEntitySnapshot>();
            _firstTrackedTimestamps = new Dictionary<long, DateTime>();
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
            _firstTrackedTimestamps.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StopTracking(long entityId)
        {
            _lagTimeSeries.RemoveSeries(entityId);
            _pinnedIds.Remove(entityId);
            _lastSources.Remove(entityId);
            _lastSnapshots.Remove(entityId);
            _firstTrackedTimestamps.Remove(entityId);
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

            // keep fresh "first timestamps"
            _firstTrackedTimestamps.RemoveRangeExceptWith(_lastSnapshots.Keys);

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

            // track entities into the time series
            foreach (var src in validSources.Values)
            {
                if (!_lagTimeSeries.ContainsKey(src.EntityId))
                {
                    _firstTrackedTimestamps[src.EntityId] = DateTime.UtcNow;
                }

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
                // ignore first N seconds into existence of an entity (even if it might be super laggy)
                // because spawning (and un-concealing) takes a lot of frame rate
                // NOTE the reason why we're not using the time series data for the "first timestamp"
                // is because we constantly dispose of old elements from the time series.
                var startTimestamp = _firstTrackedTimestamps[entityId] + _config.GracePeriodSpan;
                if (startTimestamp > DateTime.UtcNow) // grace period!
                {
                    longLags.Add(entityId, 0);
                    continue;
                }

                var scopedTimeSeries = timeSeries.GetScoped(startTimestamp);
                var longLag = CalcLongLagNormal(scopedTimeSeries);
                longLags.Add(entityId, longLag);
            }

            // pin long-laggy entities
            foreach (var (entityId, longLag) in longLags)
            {
                if (longLag >= 1)
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
                var (_, normal) = timeSeries[i]; // you can use the underscored timestamp if you want
                var outlierTest = outlierTests[i];

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