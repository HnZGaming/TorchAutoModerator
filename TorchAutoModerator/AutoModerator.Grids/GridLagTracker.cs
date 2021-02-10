using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Grids
{
    public sealed class GridLagTracker
    {
        public interface IConfig
        {
            double MaxGridMspf { get; }
            double GridWarningTime { get; }
            double GridPunishTime { get; }
            bool IsFactionExempt(string factionTag);
        }

        sealed class BridgeConfig : EntityLagTracker.IConfig
        {
            readonly IConfig _masterConfig;

            public BridgeConfig(IConfig masterConfig)
            {
                _masterConfig = masterConfig;
            }

            public double LagThreshold => _masterConfig.MaxGridMspf;
            public TimeSpan PinWindow => _masterConfig.GridWarningTime.Seconds();
            public TimeSpan PinLifeSpan => _masterConfig.GridPunishTime.Seconds();
            public bool IsFactionExempt(string factionTag) => _masterConfig.IsFactionExempt(factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityLagTracker _lagTracker;
        readonly Dictionary<long, TrackedEntitySnapshot> _playerToLaggiestGrids;

        public GridLagTracker(IConfig config)
        {
            _lagTracker = new EntityLagTracker(new BridgeConfig(config));
            _playerToLaggiestGrids = new Dictionary<long, TrackedEntitySnapshot>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetLaggiestGridOwnedBy(long ownerId, out TrackedEntitySnapshot ownedGridId)
        {
            return _playerToLaggiestGrids.TryGetValue(ownerId, out ownedGridId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerLaggiestGrids(double maxLongLagNormal)
        {
            return _playerToLaggiestGrids
                .ToTuples()
                .Where(p => p.Value.LongLagNormal > maxLongLagNormal);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerPinnedGrids()
        {
            return _playerToLaggiestGrids
                .ToTuples()
                .Where(p => p.Value.RemainingTime > TimeSpan.Zero);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            Log.Debug("updating grid lags...");

            var lags = new List<EntityLagSnapshot>();
            var laggiestGridToOwnerIds = new Dictionary<long, long>();
            var ownerIds = new HashSet<long>();

            foreach (var (grid, profileEntity) in profileResult.GetTopEntities(50))
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                var factionTag = grid.BigOwners.TryGetFirst(out var ownerId)
                    ? MySession.Static.Factions.GetPlayerFactionTag(ownerId)
                    : null;

                var lag = new EntityLagSnapshot(grid.EntityId, grid.DisplayName, mspf, factionTag);
                lags.Add(lag);

                if (!ownerIds.Contains(ownerId)) // pick the laggiest grid
                {
                    ownerIds.Add(ownerId);
                    laggiestGridToOwnerIds.Add(grid.EntityId, ownerId);
                }

                Log.Trace($"grid profiled: {grid.DisplayName} {mspf:0.00}ms/f");
            }

            _lagTracker.Update(lags);

            // update owner -> laggiest grid map w/ latest state
            foreach (var lag in _lagTracker.GetTrackedEntities(0))
            {
                if (laggiestGridToOwnerIds.TryGetValue(lag.Id, out var ownerId))
                {
                    _playerToLaggiestGrids[ownerId] = lag;
                }
            }

            var trackedGridIds = _lagTracker.GetTrackedEntityIds();
            foreach (var (ownerId, laggiestGrid) in _playerToLaggiestGrids.ToArray())
            {
                if (!trackedGridIds.Contains(laggiestGrid.Id))
                {
                    _playerToLaggiestGrids.Remove(ownerId);
                }
            }

            Log.Debug("updated grid lags");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<TrackedEntitySnapshot> GetPinnedGrids()
        {
            return _lagTracker.GetTopPins();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTracker.Clear();
            _playerToLaggiestGrids.Clear();
        }
    }
}