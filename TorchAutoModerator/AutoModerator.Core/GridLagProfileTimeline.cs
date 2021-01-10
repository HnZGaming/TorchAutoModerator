using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AutoModerator.Core
{
    public class GridLagProfileTimeline
    {
        readonly IDictionary<long, (GridLagProfileResult ProfileResult, DateTime EndTimestamp)> _self;

        public GridLagProfileTimeline()
        {
            _self = new ConcurrentDictionary<long, (GridLagProfileResult, DateTime)>();
        }

        public IEnumerable<long> GridIds => _self.Keys;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddProfileResults(IEnumerable<GridLagProfileResult> profileResults, TimeSpan remainingTime)
        {
            var endTime = DateTime.UtcNow + remainingTime;
            foreach (var profileResult in profileResults)
            {
                _self[profileResult.GridId] = (profileResult, endTime);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveExpired(DateTime timestamp)
        {
            foreach (var (gridId, (_, endTime)) in _self.ToArray())
            {
                if (endTime < timestamp)
                {
                    _self.Remove(gridId);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<GridLagReport> MakeReports(DateTime timestamp)
        {
            foreach (var (profileResult, endTime) in _self.Values)
            {
                var remainingTime = timestamp - endTime;
                var gridReport = new GridLagReport(profileResult, remainingTime);
                yield return gridReport;
            }
        }
    }
}