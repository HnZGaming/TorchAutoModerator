using System;
using System.Collections.Generic;
using System.Linq;
using Utils.General;
using Utils.General.TimeSerieses;

namespace AutoModerator.Core
{
    internal sealed class GridLagProfileTimeSeries
    {
        public interface IConfig
        {
            double Window { get; }
            double MinLifespan { get; }
            int MaxReportedGridCount { get; }

            double SampleFrequency { get; }
        }

        readonly IConfig _config;
        readonly TaggedTimeSeries<long, GridLagProfileResult> _taggedTimeSeries;

        public GridLagProfileTimeSeries(IConfig config)
        {
            _config = config;
            _taggedTimeSeries = new TaggedTimeSeries<long, GridLagProfileResult>();
        }

        public void Clear()
        {
            _taggedTimeSeries.Clear();
        }

        public void AddProfileResults(DateTime timestamp, IEnumerable<GridLagProfileResult> results)
        {
            foreach (var newProfiledGrid in results)
            {
                var tag = newProfiledGrid.GridId;
                _taggedTimeSeries.Add(tag, timestamp, newProfiledGrid);
            }
        }

        public void RemoveOldResults(DateTime timestamp)
        {
            var maxTimeSeriesLength = (_config.Window + _config.MinLifespan).Seconds();
            _taggedTimeSeries.RemoveOlderThan(timestamp - maxTimeSeriesLength);
        }

        public IEnumerable<GridLagReport> GetCurrentBroadcastableGrids()
        {
            var broadcastableGrids = new List<GridLagReport>();
            foreach (var (_, gridTimeSeries) in _taggedTimeSeries.GetTaggedTimeSeries())
            {
                if (TryGetCurrentBroadcastable(gridTimeSeries, out var broadcastableGrid))
                {
                    broadcastableGrids.Add(broadcastableGrid);
                }
            }

            return broadcastableGrids
                .OrderByDescending(r => r.ThresholdNormal)
                .Take(_config.MaxReportedGridCount);
        }

        bool TryGetCurrentBroadcastable(
            ITimeSeries<GridLagProfileResult> timeSeries,
            out GridLagReport broadcastableGrid)
        {
            broadcastableGrid = null;

            if (timeSeries.IsEmpty) return false;

            DateTime? laggySinceOrNotLaggy = null;
            DateTime? longLaggySinceOrNotLaggy = null;
            DateTime? lastLongLaggyTimestampOrNever = null;
            DateTime latestTimestamp = default;
            GridLagProfileResult latestPointGrid = null;

            foreach (var (timestamp, grid) in timeSeries.GetSeries())
            {
                var laggy = grid.ThresholdNormal >= 1f;
                if (laggy)
                {
                    if (laggySinceOrNotLaggy is DateTime laggySince)
                    {
                        var laggyTimeSpan = DateTime.UtcNow - laggySince;
                        if (laggyTimeSpan > _config.Window.Seconds())
                        {
                            longLaggySinceOrNotLaggy = timestamp;
                            lastLongLaggyTimestampOrNever = timestamp;
                        }
                    }

                    laggySinceOrNotLaggy = laggySinceOrNotLaggy ?? timestamp;
                }
                else
                {
                    laggySinceOrNotLaggy = null;
                    longLaggySinceOrNotLaggy = null;
                }

                latestTimestamp = timestamp;
                latestPointGrid = grid;
            }

            // skip grids that disappeared
            var timeSinceLatestPoint = DateTime.UtcNow - latestTimestamp;
            var disappearThresholdTime = (_config.SampleFrequency * 2).Seconds();
            if (timeSinceLatestPoint > disappearThresholdTime) return false;

            if (longLaggySinceOrNotLaggy.HasValue)
            {
                broadcastableGrid = new GridLagReport(latestPointGrid, null);
                return true;
            }

            if (lastLongLaggyTimestampOrNever is DateTime lastLongLaggyTimestamp)
            {
                var notLongLaggyTimeSpan = DateTime.UtcNow - lastLongLaggyTimestamp;
                var maxNotLongLaggyTimeSpan = _config.MinLifespan.Seconds();
                if (notLongLaggyTimeSpan < maxNotLongLaggyTimeSpan)
                {
                    var remainingTime = maxNotLongLaggyTimeSpan - notLongLaggyTimeSpan;
                    broadcastableGrid = new GridLagReport(latestPointGrid, remainingTime);
                    return true;
                }
            }

            return false;
        }
    }
}