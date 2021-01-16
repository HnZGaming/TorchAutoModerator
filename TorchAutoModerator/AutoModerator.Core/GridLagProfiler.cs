using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.World;

namespace AutoModerator.Core
{
    public sealed class GridLagProfiler : IProfiler, IDisposable
    {
        public interface IConfig
        {
            double GridMspfThreshold { get; }
            bool IgnoreNpcFactions { get; }
            IEnumerable<string> ExemptFactionTags { get; }
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

        public IEnumerable<GridLagProfileResult> GetProfileResults(int count)
        {
            var result = _gridProfiler.GetResult();

            var gridReports = new List<GridLagProfileResult>();
            foreach (var (grid, entity) in result.GetTopEntities(count))
            {
                var mspf = entity.MainThreadTime / result.TotalFrameCount;
                var normal = mspf / _config.GridMspfThreshold;
                var gridReport = GridLagProfileResult.FromGrid(grid, normal);

                if (IsExempt(gridReport)) continue;

                gridReports.Add(gridReport);
            }

            // pick top laggiest grids
            return gridReports.OrderByDescending(r => r.ThresholdNormal);
        }

        bool IsExempt(GridLagProfileResult report)
        {
            if (report.FactionTagOrNull is string factionTag)
            {
                var exemptByNpc = IsNpcFaction(factionTag) && _config.IgnoreNpcFactions;
                var exemptByTag = _config.ExemptFactionTags.Contains(factionTag.ToLower());
                return exemptByNpc || exemptByTag;
            }

            return false;
        }

        static bool IsNpcFaction(string factionTag)
        {
            var faction = MySession.Static.Factions.TryGetFactionByTag(factionTag);
            return faction?.IsEveryoneNpc() ?? false;
        }
    }
}