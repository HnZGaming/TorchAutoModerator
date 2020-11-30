using System;
using System.Collections.Generic;
using Utils.General;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Hold onto laggy grids until consistently laggy for a good length of time.
    /// </summary>
    public sealed class LaggyGridWindowBuffer
    {
        public interface IConfig
        {
            TimeSpan WindowTime { get; }
        }

        readonly IConfig _config;
        readonly LaggyGridGpsBroadcaster _gpsBroadcaster;
        readonly PersistencyObserver<long> _reports;
        DateTime? _lastCollectionTimestamp;

        public LaggyGridWindowBuffer(IConfig config, LaggyGridGpsBroadcaster gpsBroadcaster)
        {
            _config = config;
            _gpsBroadcaster = gpsBroadcaster;
            _reports = new PersistencyObserver<long>();
        }

        public void ResetCollection()
        {
            _reports.Clear();
        }

        public void UpdateCollection(IEnumerable<LaggyGridReport> laggyGrids)
        {
            laggyGrids.ThrowIfNull(nameof(laggyGrids));

            var laggyGridsMap = new Dictionary<long, LaggyGridReport>();
            foreach (var gridReport in laggyGrids)
            {
                laggyGridsMap[gridReport.GridId] = gridReport;
            }

            // update last collection timestamp
            var timeNow = DateTime.UtcNow;
            var lastCollectionTimestamp = _lastCollectionTimestamp;
            _lastCollectionTimestamp = timeNow;

            // skip the first interval
            if (!(lastCollectionTimestamp is DateTime lastTimestamp)) return;

            // remove old intervals
            var timeInterval = timeNow - lastTimestamp;
            var maxBufferSize = (int) (_config.WindowTime.TotalSeconds / timeInterval.TotalSeconds);
            _reports.CapBufferSize(maxBufferSize);

            _reports.AddInterval(laggyGridsMap.Keys);

            // broadcast "persistently" laggy grids
            var longLaggyGridIds = _reports.GetElementsPresentInAllIntervals();
            foreach (var gridId in longLaggyGridIds)
            {
                var gridReport = laggyGridsMap[gridId];
                _gpsBroadcaster.BroadcastGrid(gridReport);
            }
        }
    }
}