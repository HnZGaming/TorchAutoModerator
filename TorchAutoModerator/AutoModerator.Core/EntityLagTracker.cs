using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Utils.General;

namespace AutoModerator.Core
{
    public sealed class EntityLagTracker
    {
        public interface IConfig
        {
            double LagThreshold { get; }
            TimeSpan PinWindow { get; }
            TimeSpan PinLifeSpan { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly EntityLagTimeSeries _lagTimeSeries;
        readonly ExpiryDictionary<long> _pinnedIds;
        readonly Dictionary<long, string> _names; // for debugging only

        public EntityLagTracker(IConfig config)
        {
            _config = config;
            _lagTimeSeries = new EntityLagTimeSeries();
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

            _lagTimeSeries.AddInterval(entityLags.Select(r =>
                (r.EntityId, LagNormal: r.LagMspf / _config.LagThreshold)));

            // keep track of laggy grids & lifespan
            var laggyGridIds = _lagTimeSeries.GetLongLagNormals(1d).Select(p => p.EntityId).ToArray();
            _pinnedIds.AddOrUpdate(laggyGridIds, _config.PinLifeSpan);

            // clean up old data
            _pinnedIds.RemoveExpired();
            _lagTimeSeries.RemovePointsOlderThan(_config.PinWindow);

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

        public bool TryGetTrackedEntity(long entityId, out TrackedEntitySnapshot entity)
        {
            var hasPoint = _lagTimeSeries.TryGetLongLagNormal(entityId, out var lag);
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
            set.UnionWith(_lagTimeSeries.EntityIds);
            set.UnionWith(_pinnedIds.Keys);
            return set;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities(double minLongLagNormal)
        {
            var zip = _lagTimeSeries
                .GetLongLagNormalDictionary(minLongLagNormal)
                .Zip(_pinnedIds.ToDictionary());

            var snapshots = new List<TrackedEntitySnapshot>();
            foreach (var (entityId, (longNormal, remainingTime)) in zip)
            {
                var snapshot = new TrackedEntitySnapshot(entityId, longNormal, remainingTime);
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
                var longNormal = _lagTimeSeries.TryGetLongLagNormal(entityId, out var n) ? n : 0d;
                var snapshot = new TrackedEntitySnapshot(entityId, longNormal, remainingTime);
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
    }
}