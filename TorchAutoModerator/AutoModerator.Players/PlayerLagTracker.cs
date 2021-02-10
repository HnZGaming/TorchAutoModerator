using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Players
{
    public sealed class PlayerLagTracker
    {
        public interface IConfig
        {
            double MaxPlayerMspf { get; }
            double PlayerWarningTime { get; }
            double PlayerPunishTime { get; }
            bool IsFactionExempt(string factionTag);
        }

        sealed class BridgeConfig : EntityLagTracker.IConfig
        {
            readonly IConfig _masterConfig;

            public BridgeConfig(IConfig masterConfig)
            {
                _masterConfig = masterConfig;
            }

            public double LagThreshold => _masterConfig.MaxPlayerMspf;
            public TimeSpan PinWindow => _masterConfig.PlayerWarningTime.Seconds();
            public TimeSpan PinLifeSpan => _masterConfig.PlayerPunishTime.Seconds();
            public bool IsFactionExempt(string factionTag) => _masterConfig.IsFactionExempt(factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly EntityLagTracker _lagTracker;

        public PlayerLagTracker(IConfig config)
        {
            _config = config;
            _lagTracker = new EntityLagTracker(new BridgeConfig(config));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(BaseProfilerResult<MyIdentity> profileResult)
        {
            Log.Debug("updating player lags...");

            var results = new List<EntityLagSnapshot>();
            foreach (var (player, profilerEntry) in profileResult.GetTopEntities(50))
            {
                var mspf = profilerEntry.MainThreadTime / profileResult.TotalFrameCount;
                var faction = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
                var result = new EntityLagSnapshot(player.IdentityId, player.DisplayName, mspf, faction?.Tag);
                results.Add(result);

                Log.Trace($"player profiled: {player.DisplayName} {mspf:0.00}ms/f");
            }

            _lagTracker.Update(results);
            Log.Debug("updated player lags");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetPinnedPlayers()
        {
            return _lagTracker.GetTopPins();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetTrackedEntities(double minLagNormal)
        {
            return _lagTracker.GetTrackedEntities(minLagNormal);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTracker.Clear();
        }
    }
}