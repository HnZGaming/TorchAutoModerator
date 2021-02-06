using System;
using System.Collections.Generic;
using AutoModerator.Broadcast;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Players
{
    public sealed class LaggyPlayerTracker : LaggyEntityTracker<PlayerLagSnapshot>
    {
        public interface IConfig
        {
            double PlayerPinWindow { get; }
            double PlayerPinLifespan { get; }
            double PlayerMspfThreshold { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public LaggyPlayerTracker(IConfig config)
        {
            _config = config;
        }

        protected override TimeSpan PinWindow => _config.PlayerPinWindow.Seconds();
        protected override TimeSpan PinLifespan => _config.PlayerPinLifespan.Seconds();
        protected override bool IsFactionExempt(string factionTag) => _config.IsFactionExempt(factionTag);

        public void Update(BaseProfilerResult<MyIdentity> playerProfileResult, BaseProfilerResult<MyCubeGrid> gridProfileResult)
        {
            // map player -> grid id
            var laggiestGrids = new Dictionary<long, long>();
            foreach (var (grid, _) in gridProfileResult.GetTopEntities(50))
            {
                if (!grid.BigOwners.TryGetFirst(out var playerId)) continue;
                if (laggiestGrids.ContainsKey(playerId)) continue;
                laggiestGrids[playerId] = grid.EntityId;
            }

            var laggiestPlayerMspf = double.MinValue;

            var snapshots = new List<PlayerLagSnapshot>();
            foreach (var (player, profilerEntry) in playerProfileResult.GetTopEntities(50))
            {
                var mspf = profilerEntry.MainThreadTime / playerProfileResult.TotalTime;
                var lag = mspf / _config.PlayerMspfThreshold;
                var gridId = laggiestGrids.GetValueOrDefault(player.IdentityId, 0);
                var snapshot = PlayerLagSnapshot.FromPlayer(player, lag, gridId);
                snapshots.Add(snapshot);

                laggiestPlayerMspf = Math.Max(laggiestPlayerMspf, mspf);
            }

            Log.Trace($"laggiest player mspf: {laggiestPlayerMspf:0.000}");
            Update(snapshots);
        }

        public IEnumerable<IEntityGpsSource> CreateGpsSources(PlayerGpsSource.IConfig config, int count)
        {
            foreach (var ((snapshot, remainingTime), rank) in GetTopPins(count).Indexed())
            {
                var gpsSource = new PlayerGpsSource(config, snapshot, remainingTime, rank);
                Log.Trace($"player gps source: {gpsSource}");
                yield return gpsSource;
            }
        }
    }
}