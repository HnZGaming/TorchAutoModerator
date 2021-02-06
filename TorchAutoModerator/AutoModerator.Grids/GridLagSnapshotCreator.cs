using System;
using System.Collections.Generic;
using NLog;
using Profiler.Basics;
using Sandbox.Game.Entities;

namespace AutoModerator.Grids
{
    public sealed class GridLagSnapshotCreator
    {
        public interface IConfig
        {
            double GridMspfThreshold { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public GridLagSnapshotCreator(IConfig config)
        {
            _config = config;
        }

        public IEnumerable<GridLagSnapshot> CreateLagSnapshots(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            foreach (var (grid, profileEntity) in profileResult.GetTopEntities(50))
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                var lag = mspf / _config.GridMspfThreshold;
                var snapshot = GridLagSnapshot.FromGrid(grid, lag);
                yield return snapshot;
            }
        }
    }
}