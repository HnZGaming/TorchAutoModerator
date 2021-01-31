using System;
using System.Collections.Generic;
using AutoModerator.Core;
using NLog;
using Profiler.Basics;
using Sandbox.Game.Entities;
using Utils.General;

namespace AutoModerator.Grids
{
    public sealed class LaggyGridTracker : LaggyEntityTracker<GridLagSnapshot>
    {
        public interface IConfig
        {
            double GridPinWindow { get; }
            double GridPinLifespan { get; }
            double GridMspfThreshold { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public LaggyGridTracker(IConfig config)
        {
            _config = config;
        }

        protected override TimeSpan PinWindow => _config.GridPinWindow.Seconds();
        protected override TimeSpan PinLifespan => _config.GridPinLifespan.Seconds();
        protected override bool IsFactionExempt(string factionTag) => _config.IsFactionExempt(factionTag);

        public void Update(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            var snapshots = new List<GridLagSnapshot>();
            foreach (var (grid, profileEntity) in profileResult.GetTopEntities())
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                var normal = mspf / _config.GridMspfThreshold;
                var snapshot = GridLagSnapshot.FromGrid(grid, normal);
                snapshots.Add(snapshot);
            }

            Update(snapshots);
        }

        public IEnumerable<IEntityGpsSource> CreateGpsSources(GridGpsSource.IConfig config, int count)
        {
            foreach (var ((snapshot, remainingTime), rank) in GetTopPins(count).Indexed())
            {
                var gpsSource = new GridGpsSource(config, snapshot, remainingTime, rank);
                Log.Trace($"grid gps source: {gpsSource}");
                yield return gpsSource;
            }
        }
    }
}