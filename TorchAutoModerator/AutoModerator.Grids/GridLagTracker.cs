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
        readonly Dictionary<long, TrackedEntitySnapshot> _ownerToLaggiestGrids;

        public GridLagTracker(IConfig config)
        {
            _lagTracker = new EntityLagTracker(new BridgeConfig(config));
            _ownerToLaggiestGrids = new Dictionary<long, TrackedEntitySnapshot>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetLaggiestGridOwnedBy(long ownerId, out TrackedEntitySnapshot ownedGridId)
        {
            return _ownerToLaggiestGrids.TryGetValue(ownerId, out ownedGridId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerLaggiestGrids(double maxLongLagNormal)
        {
            return _ownerToLaggiestGrids
                .Where(p => p.Value.LongLagNormal > maxLongLagNormal)
                .ToTuples();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerPinnedGrids()
        {
            return _ownerToLaggiestGrids
                .Where(p => p.Value.RemainingTime > TimeSpan.Zero)
                .ToTuples();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            Log.Debug("updating grid lags...");

            var lags = new List<EntityLagSnapshot>();
            var latestLaggiestGridToOwnerIds = new Dictionary<long, long>();
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
                    latestLaggiestGridToOwnerIds.Add(grid.EntityId, ownerId);
                }

                Log.Trace($"grid profiled: {grid.DisplayName} {mspf:0.00}ms/f");
            }

            _lagTracker.Update(lags);

            // update owner -> laggiest grid map w/ latest state
            foreach (var lag in _lagTracker.GetTrackedEntities(0))
            {
                if (latestLaggiestGridToOwnerIds.TryGetValue(lag.Id, out var ownerId))
                {
                    _ownerToLaggiestGrids[ownerId] = lag;
                }
            }

            var trackedGridIds = _lagTracker.GetTrackedEntityIds().ToSet();
            foreach (var (ownerId, laggiestGrid) in _ownerToLaggiestGrids.ToArray())
            {
                if (!trackedGridIds.Contains(laggiestGrid.Id))
                {
                    _ownerToLaggiestGrids.Remove(ownerId);
                    Log.Trace($"removed untracked owner from owner map: {ownerId}");
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
            _ownerToLaggiestGrids.Clear();
        }
    }
}