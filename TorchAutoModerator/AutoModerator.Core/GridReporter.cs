using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Core
{
    public sealed class GridReporter
    {
        public interface IConfig
        {
            float ThresholdMspf { get; }
            bool ReportNpcFactions { get; }
            IEnumerable<string> ExemptFactionTags { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;

        public GridReporter(IConfig config)
        {
            _config = config;
        }

        public async Task<IEnumerable<GridReport>> Profile(TimeSpan profileTime, CancellationToken canceller)
        {
            Log.Debug("Scanning grids...");

            var gridReports = new List<GridReport>();

            var mask = new GameEntityMask(null, null, null);
            using (var gridProfiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(gridProfiler))
            {
                gridProfiler.MarkStart();

                // profile the world for some time
                await Task.Delay(profileTime, canceller);

                // done
                var result = gridProfiler.GetResult();

                foreach (var (grid, entity) in result.GetTopEntities())
                {
                    var mspf = entity.MainThreadTime / result.TotalFrameCount;
                    var normal = mspf / _config.ThresholdMspf;
                    var gridReport = GridReport.FromGrid(grid, normal);
                    gridReports.Add(gridReport);
                }
            }

            // pick top laggiest grids
            var topGridReports = gridReports
                .Where(r => !IsExempt(r))
                .FilterUniqueByKey(r => r.GridId)
                .OrderByDescending(r => r.ThresholdNormal)
                .Take(20) // just small enough number of grids so that the time series won't flood
                .ToArray();

            Log.Debug($"Laggy grids: {topGridReports.ToStringSeq()}");
            return topGridReports;
        }

        bool IsExempt(GridReport report)
        {
            if (report.FactionTagOrNull is string factionTag)
            {
                var exemptByNpc = IsNpcFaction(factionTag) && _config.ReportNpcFactions;
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