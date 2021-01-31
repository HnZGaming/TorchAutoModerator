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
        public interface IConfig
        {
            TimeSpan PinWindow { get; }
            TimeSpan PinLifespan { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly EntityLagTimeSeries _lagTimeSeries;
        readonly LifespanDictionary<long> _pinnedGridIds;
        readonly Dictionary<long, GridLagProfileResult> _lastProfileResults;

        public GridLagMonitor(IConfig config)
        {
            _config = config;
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
            var trackedGridIds = _lagTimeSeries.EntityIds.Concat(_pinnedGridIds.Keys);
            _lastProfileResults.RemoveRangeExceptWith(trackedGridIds);

            // keep track of laggy grids & gps lifespan
            var laggyGridIds = _lagTimeSeries.GetLaggyEntityIds().ToArray();
            _pinnedGridIds.AddOrUpdate(laggyGridIds);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update()
        {
            _lagTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - _config.PinWindow);

            _pinnedGridIds.Lifespan = _config.PinLifespan;
            _pinnedGridIds.RemoveExpired();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<GridGpsSource> CreateGpsSources(GridGpsSource.IConfig config)
        {
            _pinnedGridIds.Lifespan = _config.PinLifespan;
            foreach (var (gridId, remainingTime) in _pinnedGridIds.GetRemainingTimes())
            {
                var lastProfileResult = _lastProfileResults[gridId];
                var gpsSource = new GridGpsSource(config, lastProfileResult, remainingTime);
                Log.Trace($"grid gps source: {gpsSource}");

                yield return gpsSource;
            }
        }
    }
}