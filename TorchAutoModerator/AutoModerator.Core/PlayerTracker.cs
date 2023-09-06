extern alias ProfilerAlias;
using System;
using System.Collections.Generic;
using NLog;
using ProfilerAlias::Profiler.Basics;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Core
{
    public sealed class PlayerTracker
    {
        public interface IConfig
        {
            double MaxPlayerMspf { get; }
            double TrackingTime { get; }
            double PunishTime { get; }
            double GracePeriodTime { get; }
            double OutlierFenceNormal { get; }
            bool IsIdentityExempt(long identityId);
        }

        sealed class ConfigProxy : EntityTracker.IConfig
        {
            readonly IConfig _masterConfig;

            public ConfigProxy(IConfig masterConfig)
            {
                _masterConfig = masterConfig;
            }

            public double PinLag => _masterConfig.MaxPlayerMspf;
            public double OutlierFenceNormal => _masterConfig.OutlierFenceNormal;
            public TimeSpan TrackingSpan => _masterConfig.TrackingTime.Seconds();
            public TimeSpan PinSpan => _masterConfig.PunishTime.Seconds();
            public TimeSpan GracePeriodSpan => _masterConfig.GracePeriodTime.Seconds();
            public bool IsIdentityExempt(long factionId) => _masterConfig.IsIdentityExempt(factionId);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityTracker _entityTracker;

        public PlayerTracker(IConfig config)
        {
            _entityTracker = new EntityTracker(new ConfigProxy(config));
        }

        public IReadOnlyDictionary<long, TrackedEntity> Entities => _entityTracker.Entities;

        public void Update(BaseProfilerResult<MyIdentity> profileResult)
        {
            Log.Trace("updating player lags...");

            var results = new List<EntitySource>();
            foreach (var (player, profilerEntry) in profileResult.GetTopEntities(20))
            {
                var mspf = profilerEntry.MainThreadTime / profileResult.TotalFrameCount;
                var faction = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
                var factionId = faction?.FactionId ?? 0L;
                var factionTag = faction?.Tag ?? "<single>";
                var result = new EntitySource(player.IdentityId, player.DisplayName, player.IdentityId, player.DisplayName, factionId, factionTag, mspf);
                results.Add(result);
            }

            _entityTracker.Update(results);
            Log.Trace("updated player lags");
        }

        public void Clear()
        {
            _entityTracker.Clear();
        }
    }
}