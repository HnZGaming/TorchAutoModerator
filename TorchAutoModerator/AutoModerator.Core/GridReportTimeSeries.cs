using System;
using System.Collections.Generic;
using Utils.General;

namespace AutoModerator.Core
{
    public sealed class GridReportTimeSeries
    {
        readonly TimeSeries<GridReport> _reportsTimeSeries;

        public GridReportTimeSeries()
        {
            _reportsTimeSeries = new TimeSeries<GridReport>();
        }

        public void AddReports(IEnumerable<GridReport> reports)
        {
            _reportsTimeSeries.Add(DateTime.UtcNow, reports);
        }

        public IEnumerable<(DateTime, GridReport)> GetReportsSince(DateTime thresholdTimestamp)
        {
            return _reportsTimeSeries.GetElementsSince(thresholdTimestamp);
        }

        public void RemoveReportsOlderThan(DateTime thresholdTimestamp)
        {
            _reportsTimeSeries.RemoveOlderThan(thresholdTimestamp);
        }

        public void Clear()
        {
            _reportsTimeSeries.Clear();
        }
    }
}