using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoModerator.Core;
using NLog;
using Utils.General;

namespace AutoModerator.Grids
{
    public sealed class GridLagMonitor
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly EntityLagTimeSeries _lagTimeSeries;
        readonly LifespanDictionary<long> _pinnedGridIds;
        readonly Dictionary<long, GridLagProfileResult> _lastProfileResults;

        public GridLagMonitor()
        {
            _lagTimeSeries = new EntityLagTimeSeries();
            _pinnedGridIds = new LifespanDictionary<long>();
            _lastProfileResults = new Dictionary<long, GridLagProfileResult>();
        }

        public int PinnedGridCount => _pinnedGridIds.Count;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _lagTimeSeries.Clear();
            _pinnedGridIds.Clear();
            _lastProfileResults.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddProfileInterval(IEnumerable<GridLagProfileResult> profileResults)
        {
            _lagTimeSeries.AddInterval(profileResults.Select(r => (r.GridId, r.ThresholdNormal)));

            // map grid id -> last profile result
            _lastProfileResults.AddRangeWithKeys(profileResults, r => r.GridId);

            // expire old data
            var trackedGridIds = CollectionUtils.Merge(
                _lagTimeSeries.EntityIds,
                _pinnedGridIds.Keys);
            _lastProfileResults.RemoveRangeExceptWith(trackedGridIds);

            // keep track of laggy grids & gps lifespan
            var laggyGridIds = _lagTimeSeries.GetLaggyEntityIds().ToArray();
            _pinnedGridIds.AddOrUpdate(laggyGridIds);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemovePointsOlderThan(TimeSpan period)
        {
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - period);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveGpsSourcesOlderThan(TimeSpan period)
        {
            _pinnedGridIds.RemoveExpired(period);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<GridGpsSource> CreateGpsSources(GridGpsSource.IConfig config)
        {
            foreach (var (gridId, remainingTime) in _pinnedGridIds.GetRemainingTimes())
            {
                var lastProfileResult = _lastProfileResults[gridId];
                var gpsSource = new GridGpsSource(config, lastProfileResult, remainingTime);
                yield return gpsSource;
            }
        }
    }
}