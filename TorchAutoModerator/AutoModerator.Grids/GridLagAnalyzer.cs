using System.Collections.Generic;
using Profiler.Basics;
using Sandbox.Game.Entities;

namespace AutoModerator.Grids
{
    public sealed class GridLagAnalyzer
    {
        public interface IConfig
        {
            double GridMspfThreshold { get; }
            int MaxProfiledGridCount { get; }
            bool IsFactionExempt(string factionTag);
        }

        readonly IConfig _config;

        public GridLagAnalyzer(IConfig config)
        {
            _config = config;
        }

        public IEnumerable<GridLagProfileResult> Analyze(BaseProfilerResult<MyCubeGrid> profileResult)
        {
            var results = new List<GridLagProfileResult>();
            foreach (var (grid, profileEntity) in profileResult.GetTopEntities())
            {
                var mspf = profileEntity.MainThreadTime / profileResult.TotalFrameCount;
                var normal = mspf / _config.GridMspfThreshold;
                var result = GridLagProfileResult.FromGrid(grid, normal);

                if (result.FactionTagOrNull is string factionTag &&
                    _config.IsFactionExempt(factionTag))
                {
                    continue;
                }

                results.Add(result);

                if (results.Count >= _config.MaxProfiledGridCount)
                {
                    break;
                }
            }

            return results;
        }
    }
}