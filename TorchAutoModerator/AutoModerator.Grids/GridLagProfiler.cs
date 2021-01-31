using System;
using System.Collections.Generic;
using NLog;
using Profiler.Basics;
using Profiler.Core;

namespace AutoModerator.Grids
{
    public sealed class GridLagProfiler : IProfiler, IDisposable
    {
        public interface IConfig
        {
            double MspfThreshold { get; }
            bool IsFactionExempt(string factionTag);
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly GridProfiler _gridProfiler;

        public GridLagProfiler(IConfig config, GameEntityMask mask)
        {
            _config = config;
            _gridProfiler = new GridProfiler(mask);
        }

        void IProfiler.ReceiveProfilerResult(in ProfilerResult profilerResult)
        {
            ((IProfiler) _gridProfiler).ReceiveProfilerResult(profilerResult);
        }

        void IDisposable.Dispose()
        {
            _gridProfiler.Dispose();
        }

        public void MarkStart()
        {
            _gridProfiler.MarkStart();
        }

        public IEnumerable<GridLagProfileResult> GetTopProfileResults(int count)
        {
            var results = new List<GridLagProfileResult>();
            foreach (var (grid, entity) in _gridProfiler.GetResult().GetTopEntities())
            {
                var mspf = entity.MainThreadTime / _gridProfiler.GetResult().TotalFrameCount;
                var normal = mspf / _config.MspfThreshold;
                var result = GridLagProfileResult.FromGrid(grid, normal);

                if (result.FactionTagOrNull is string factionTag &&
                    _config.IsFactionExempt(factionTag))
                {
                    continue;
                }

                results.Add(result);

                if (results.Count >= count)
                {
                    break;
                }
            }

            return results;
        }
    }
}