using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Utils.General;

namespace AutoModerator.Core
{
    public sealed class EntityTracker
    {
        public interface IConfig
        {
            double PinLag { get; }
            double OutlierFenceNormal { get; }
            TimeSpan TrackingSpan { get; }
            TimeSpan PinSpan { get; }
            TimeSpan GracePeriodSpan { get; }
            bool IsIdentityExempt(long factionId);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly ConcurrentDictionary<long, TrackedEntity> _entities;

        public EntityTracker(IConfig config)
        {
            _config = config;
            _entities = new ConcurrentDictionary<long, TrackedEntity>();
        }

        public IReadOnlyDictionary<long, TrackedEntity> Entities => _entities;

        public void Clear()
        {
            _entities.Clear();
        }

        public void StopTracking(long entityId)
        {
            _entities.Remove(entityId);
        }

        public void Update(IReadOnlyList<EntitySource> sources)
        {
            // find valid entities
            var validSources = sources
                .Where(s => s.LagMspf.IsValid())
                .ToDictionary(p => p.EntityId);

            if (validSources.Count < sources.Count)
            {
                Log.Warn("invalid ms/f value(s) found; maybe: server freezing");
            }

            validSources.RemoveWhere((_, v) => 
                _config.IsIdentityExempt(v.OwnerId));

            // make a new tracker
            foreach (var (entityId, _) in validSources)
            {
                if (!_entities.ContainsKey(entityId))
                {
                    _entities[entityId] = new TrackedEntity(_config, entityId);
                }
            }

            // update existing trackers
            foreach (var (entityId, entity) in _entities)
            {
                if (validSources.TryGetValue(entityId, out var src))
                {
                    entity.Update(src);
                }
                else
                {
                    entity.Update(null);
                }
            }

            if (Log.IsDebugEnabled)
            {
                var allTrackedEntities = _entities.ToArray(); // including pins
                if (!allTrackedEntities.Any())
                {
                    Log.Debug("tracking 0 entities");
                }
                else
                {
                    foreach (var (_, entity) in allTrackedEntities)
                    {
                        Log.Debug($"tracking: {entity}");
                    }
                }
            }
        }
    }
}