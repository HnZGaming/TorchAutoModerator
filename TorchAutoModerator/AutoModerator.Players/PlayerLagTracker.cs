using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.World;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Players
{
    public sealed class PlayerLagTracker
    {
        public interface IConfig
        {
            double MaxPlayerMspf { get; }
            double TrackingTime { get; }
            double PunishTime { get; }
            double OutlierFenceNormal { get; }
            bool IsFactionExempt(long factionId);
        }

        sealed class BridgeConfig : EntityLagTracker.IConfig
        {
            readonly IConfig _masterConfig;

            public BridgeConfig(IConfig masterConfig)
            {
                _masterConfig = masterConfig;
            }

            public double PinLag => _masterConfig.MaxPlayerMspf;
            public double OutlierFenceNormal => _masterConfig.OutlierFenceNormal;
            public TimeSpan TrackingSpan => _masterConfig.TrackingTime.Seconds();
            public TimeSpan PinSpan => _masterConfig.PunishTime.Seconds();
            public bool IsFactionExempt(long factionId) => _masterConfig.IsFactionExempt(factionId);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityLagTracker _lagTracker;

        public PlayerLagTracker(IConfig config)
        {
            _lagTracker = new EntityLagTracker(new BridgeConfig(config));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(BaseProfilerResult<MyIdentity> profileResult)
        {
            Log.Trace("updating player lags...");

            var results = new List<EntityLagSource>();
            foreach (var (player, profilerEntry) in profileResult.GetTopEntities(50))
            {
                var mspf = profilerEntry.MainThreadTime / profileResult.TotalFrameCount;
                var factionId = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId)?.FactionId ?? 0L;
                var result = new EntityLagSource(player.IdentityId, player.DisplayName, player.IdentityId, player.DisplayName, mspf, factionId);
                results.Add(result);
            }

            _lagTracker.Update(results);
            Log.Trace("updated player lags");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetPinnedPlayers()
        {
            return _lagTracker.GetTopPins();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities(double lagNormal)
        {
            return _lagTracker
                .GetTrackedEntities()
                .Where(e => e.LongLagNormal >= lagNormal)
                .ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities()
        {
            return _lagTracker.GetTrackedEntities();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTracker.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetTimeSeries(long entityId, out ITimeSeries<double> timeSeries)
        {
            return _lagTracker.TryGetTimeSeries(entityId, out timeSeries);
        }
    }
}