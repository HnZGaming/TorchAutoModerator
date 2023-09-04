extern alias ProfilerAlias;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using ProfilerAlias::Profiler.Basics;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Core
{
    public sealed class GridTracker
    {
        public interface IConfig
        {
            double MaxGridMspf { get; }
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

            public double PinLag => _masterConfig.MaxGridMspf;
            public double OutlierFenceNormal => _masterConfig.OutlierFenceNormal;
            public TimeSpan TrackingSpan => _masterConfig.TrackingTime.Seconds();
            public TimeSpan PinSpan => _masterConfig.PunishTime.Seconds();
            public TimeSpan GracePeriodSpan => _masterConfig.GracePeriodTime.Seconds();
            public bool IsIdentityExempt(long id) => _masterConfig.IsIdentityExempt(id);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityTracker _entityTracker;
        readonly ConcurrentDictionary<long, long> _ownersToLaggiestGrids;

        public GridTracker(IConfig config)
        {
            _entityTracker = new EntityTracker(new ConfigProxy(config));
            _ownersToLaggiestGrids = new ConcurrentDictionary<long, long>();
        }

        public IReadOnlyDictionary<long, TrackedEntity> Entities => _entityTracker.Entities;

        public bool TryGetLaggiestGridOwnedBy(long ownerId, out TrackedEntity grid)
        {
            grid = default;
            return _ownersToLaggiestGrids.TryGetValue(ownerId, out var gridId) &&
                   Entities.TryGetValue(gridId, out grid);
        }

        public bool TryFindGridOwnerByName(string ownerName, out (long PlayerId, string PlayerName) grid)
        {
            if (Entities.Values.TryGetFirst(e => e.OwnerName == ownerName, out var g))
            {
                grid = (g.OwnerId, g.OwnerName);
                return true;
            }

            grid = default;
            return false;
        }

        public Dictionary<long, TrackedEntity> GetLaggiestGridsByOwners()
        {
            var dic = new Dictionary<long, TrackedEntity>();
            foreach (var (ownerId, laggiestGridId) in _ownersToLaggiestGrids)
            {
                if (!Entities.TryGetValue(laggiestGridId, out var laggiestGrid))
                {
                    Log.Warn($"grid deleted by player: {laggiestGridId} by {ownerId}");
                    continue;
                }

                dic[ownerId] = laggiestGrid;
            }

            return dic;
        }

        public Dictionary<long, TrackedEntity> GetPinnedGridsByOwners()
        {
            var dic = new Dictionary<long, TrackedEntity>();
            foreach (var (ownerId, laggiestGridId) in _ownersToLaggiestGrids)
            {
                if (!Entities.TryGetValue(laggiestGridId, out var grid))
                {
                    Log.Warn($"grid deleted by player: {laggiestGridId} by {ownerId}");
                    continue;
                }

                if (grid.IsPinned)
                {
                    dic[ownerId] = grid;
                }
            }

            return dic;
        }

        public void Update(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            Log.Trace("updating grid lags...");

            var sources = new List<EntitySource>();
            foreach (var (grid, profileEntity) in profileResult.GetTopEntities(20))
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                if (TryCreateEntitySource(grid, mspf, out var source))
                {
                    sources.Add(source);
                }
            }

            _entityTracker.Update(sources);

            _ownersToLaggiestGrids.Clear();
            foreach (var entity in Entities.Values.OrderBy(TrackedEntityUtils.GetLongLagNormal))
            {
                _ownersToLaggiestGrids[entity.OwnerId] = entity.Id;
            }

            Log.Trace("updated grid lags");
        }

        static bool TryCreateEntitySource(MyCubeGrid grid, double mspf, out EntitySource source)
        {
            source = default;

            var ownerIds = grid.BigOwners.Concat(grid.SmallOwners);
            if (!ownerIds.TryGetFirst(out var ownerId)) return false;

            var faction = MySession.Static.Factions.GetPlayerFaction(ownerId);
            var factionId = faction?.FactionId ?? 0L;
            var factionTag = faction?.Tag ?? "<single>";
            var ownerName = MySession.Static.Players.TryGetPlayerById(ownerId, out var p) ? p.DisplayName : $"<{ownerId}>";
            source = new EntitySource(grid.EntityId, grid.DisplayName, ownerId, ownerName, factionId, factionTag, mspf);
            return true;
        }

        public void Clear()
        {
            _entityTracker.Clear();
            _ownersToLaggiestGrids.Clear();
        }

        public void StopTracking(long gridId)
        {
            _entityTracker.StopTracking(gridId);
        }
    }
}