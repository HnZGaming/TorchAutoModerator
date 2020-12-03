using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using TorchShittyShitShitter.Core.Scanners;
using Utils.General;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Find laggy grids in the game.
    /// </summary>
    public sealed class LaggyGridFinder
    {
        public interface IConfig
        {
            int MaxReportCountPerScan { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;
        readonly List<ILagScanner> _subScanners;

        public LaggyGridFinder(IConfig config, IEnumerable<ILagScanner> subScanners)
        {
            _config = config;
            _subScanners = subScanners.ToList();
        }

        public async Task<IEnumerable<LaggyGridReport>> ScanLaggyGrids(CancellationToken canceller)
        {
            Log.Debug("Scanning laggy grids...");

            var reports = new List<LaggyGridReport>();

            var mask = new GameEntityMask(null, null, null);
            using (var profiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(profiler))
            {
                profiler.MarkStart();

                // profile the world for some time
                await canceller.Delay(5.Seconds());

                var profiledGrids = new List<(MyCubeGrid, double)>();
                var result = profiler.GetResult();
                foreach (var (grid, entity) in result.GetTopEntities())
                {
                    var mspf = entity.MainThreadTime / result.TotalFrameCount;
                    profiledGrids.Add((grid, mspf));
                }

                var scanStartTick = Stopwatch.GetTimestamp();

                // scan
                foreach (var subScanner in _subScanners)
                {
                    try
                    {
                        var subReports = subScanner.Scan(profiledGrids);
                        reports.AddRange(subReports);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }

                var scanTime = (Stopwatch.GetTimestamp() - scanStartTick) / 10000D;
                Log.Trace($"Done scanning ({scanTime:0.00}ms spent)");
            }

            // pick top laggiest grids
            var topReports = reports
                .FilterUniqueByKey(r => r.GridId)
                .OrderByDescending(r => r.Mspf)
                .Take(_config.MaxReportCountPerScan)
                .ToArray();

            Log.Debug($"Laggy grids: {topReports.ToStringSeq()}");
            return topReports;
        }
    }
}