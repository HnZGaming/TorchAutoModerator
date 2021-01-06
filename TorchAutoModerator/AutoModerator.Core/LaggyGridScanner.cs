using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoModerator.Core
{
    public sealed class LaggyGridScanner
    {
        public interface IConfig
        {
            TimeSpan WindowTime { get; }
            int MaxReportSizePerScan { get; }
        }

        readonly IConfig _config;
        readonly GridReportTimeSeries _gridReportTimeSeries;

        public LaggyGridScanner(IConfig config, GridReportTimeSeries timeSeries)
        {
            _config = config;
            _gridReportTimeSeries = timeSeries;
        }

        public IEnumerable<GridReport> ScanLaggyGrids()
        {
            var thresholdTimestamp = DateTime.UtcNow - _config.WindowTime;
            var gridReports = _gridReportTimeSeries.GetReportsSince(thresholdTimestamp);

            var gridReportMap = new Dictionary<long, List<(DateTime, GridReport)>>();
            foreach (var (timestamp, gridReport) in gridReports)
            {
                if (!gridReportMap.TryGetValue(gridReport.GridId, out var list))
                {
                    list = new List<(DateTime, GridReport)>();
                    gridReportMap[gridReport.GridId] = list;
                }

                list.Add((timestamp, gridReport));
            }

            var laggyGridReports = new List<GridReport>();
            foreach (var (_, reports) in gridReportMap)
            {
                var normal = reports.Average(r => r.Item2.ThresholdNormal);
                if (normal > 1f)
                {
                    var referenceReport = reports.Last().Item2;
                    laggyGridReports.Add(referenceReport);
                }
            }

            return laggyGridReports
                .OrderByDescending(r => r.ThresholdNormal)
                .Take(_config.MaxReportSizePerScan);
        }
    }
}