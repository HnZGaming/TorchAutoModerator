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
            double GridTrackingTime { get; }
            double GridPunishTime { get; }
            double OutlierFenceNormal { get; }
            bool IsFactionExempt(string factionTag);
        }

        sealed class BridgeConfig : EntityLagTracker.IConfig
        {
            readonly IConfig _masterConfig;

            public BridgeConfig(IConfig masterConfig)
            {
                _masterConfig = masterConfig;
            }

            public double PinLag => _masterConfig.MaxGridMspf;
            public double OutlierFenceNormal=> _masterConfig.OutlierFenceNormal;
            public TimeSpan TrackingSpan => _masterConfig.GridTrackingTime.Seconds();
            public TimeSpan PinSpan => _masterConfig.GridPunishTime.Seconds();
            public bool IsFactionExempt(string factionTag) => _masterConfig.IsFactionExempt(factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityLagTracker _lagTracker;
        readonly Dictionary<long, long> _ownerToLaggiestGrids;

        public GridLagTracker(IConfig config)
        {
            _lagTracker = new EntityLagTracker(new BridgeConfig(config));
            _ownerToLaggiestGrids = new Dictionary<long, long>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetLaggiestGridOwnedBy(long ownerId, out TrackedEntitySnapshot grid)
        {
            grid = default;
            return _ownerToLaggiestGrids.TryGetValue(ownerId, out var gridId) &&
                   _lagTracker.TryGetTrackedEntity(gridId, out grid);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerLaggiestGrids(double maxLongLagNormal)
        {
            foreach (var (ownerId, laggiestGridId) in _ownerToLaggiestGrids)
            {
                if (!_lagTracker.TryGetTrackedEntity(laggiestGridId, out var grid))
                {
                    Log.Warn($"grid deleted by player: {grid.Id} by {ownerId}");
                    continue;
                }

                if (grid.LongLagNormal < maxLongLagNormal) continue;

                yield return (ownerId, grid);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long OwnerId, TrackedEntitySnapshot Grid)> GetPlayerPinnedGrids()
        {
            foreach (var (ownerId, laggiestGridId) in _ownerToLaggiestGrids)
            {
                if (!_lagTracker.TryGetTrackedEntity(laggiestGridId, out var grid))
                {
                    Log.Warn($"grid deleted by player: {grid.Id} by {ownerId}");
                    continue;
                }

                if (!grid.IsPinned) continue;

                yield return (ownerId, grid);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            Log.Debug("updating grid lags...");

            var lags = new List<EntityLagSource>();
            var latestLaggiestGridToOwnerIds = new Dictionary<long, long>();
            var ownerIds = new HashSet<long>();

            foreach (var (grid, profileEntity) in profileResult.GetTopEntities(50))
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                var factionTag = grid.BigOwners.TryGetFirst(out var ownerId)
                    ? MySession.Static.Factions.GetPlayerFactionTag(ownerId)
                    : null;

                var lag = new EntityLagSource(grid.EntityId, grid.DisplayName, mspf, factionTag);
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
            foreach (var trackedGrid in _lagTracker.GetTrackedEntities(0))
            {
                if (latestLaggiestGridToOwnerIds.TryGetValue(trackedGrid.Id, out var ownerId))
                {
                    _ownerToLaggiestGrids[ownerId] = trackedGrid.Id;
                }
            }

            var trackedGridIds = _lagTracker.GetTrackedEntityIds().ToSet();
            foreach (var (ownerId, laggiestGridId) in _ownerToLaggiestGrids.ToArray())
            {
                if (!trackedGridIds.Contains(laggiestGridId))
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