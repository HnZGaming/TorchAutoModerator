using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoModerator.Core.Scanners;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Core
{
    /// <summary>
    /// Find laggy grids in the game.
    /// </summary>
    public sealed class LaggyGridFinder
    {
        public interface IConfig
        {
            int MaxReportCountPerScan { get; }
            bool ExemptNpcFactions { get; }
            IEnumerable<string> ExemptFactionTags { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;
        readonly List<ILagScanner> _scanners;
        readonly HashSet<string> _exemptFactionTags;

        public LaggyGridFinder(IConfig config, IEnumerable<ILagScanner> scanners)
        {
            _config = config;
            _scanners = scanners.ToList();
            _exemptFactionTags = new HashSet<string>();
        }

        public async Task<IEnumerable<LaggyGridReport>> ScanLaggyGrids(TimeSpan profileTime, CancellationToken canceller)
        {
            Log.Debug("Scanning laggy grids...");

            var reports = new ConcurrentQueue<LaggyGridReport>();

            var mask = new GameEntityMask(null, null, null);
            using (var profiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(profiler))
            {
                profiler.MarkStart();

                // profile the world for some time
                await Task.Delay(profileTime, canceller);

                var profiledGrids = new List<(MyCubeGrid, double)>();
                var result = profiler.GetResult();
                foreach (var (grid, entity) in result.GetTopEntities())
                {
                    var mspf = entity.MainThreadTime / result.TotalFrameCount;
                    profiledGrids.Add((grid, mspf));
                }

                var scanStartTick = Stopwatch.GetTimestamp();

                // scan
                Parallel.ForEach(_scanners, scanner =>
                {
                    var scan = scanner.Scan(profiledGrids);
                    foreach (var report in scan)
                    {
                        reports.Enqueue(report);
                    }
                });

                var scanTime = (Stopwatch.GetTimestamp() - scanStartTick) / 10000D;
                Log.Trace($"Done scanning ({scanTime:0.00}ms spent)");
            }

            UpdateExemptList();

            // pick top laggiest grids
            var topReports = reports
                .Where(r => !IsExempt(r))
                .FilterUniqueByKey(r => r.GridId)
                .OrderByDescending(r => r.Mspf)
                .Take(_config.MaxReportCountPerScan)
                .ToArray();

            Log.Debug($"Laggy grids: {topReports.ToStringSeq()}");
            return topReports;
        }

        void UpdateExemptList()
        {
            _exemptFactionTags.Clear();
            foreach (var exemptFactionTag in _config.ExemptFactionTags)
            {
                _exemptFactionTags.Add(exemptFactionTag.ToLower());
            }
        }

        bool IsExempt(LaggyGridReport report)
        {
            if (report.FactionTagOrNull is string factionTag)
            {
                var exemptByNpc = IsNpcFaction(factionTag) && _config.ExemptNpcFactions;
                var exemptByTag = _exemptFactionTags.Contains(factionTag.ToLower());
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