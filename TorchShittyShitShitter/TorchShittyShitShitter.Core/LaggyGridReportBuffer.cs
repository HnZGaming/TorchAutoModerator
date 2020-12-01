using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Utils.General;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Hold onto laggy grids until consistently laggy for a good length of time.
    /// </summary>
    public sealed class LaggyGridReportBuffer
    {
        public interface IConfig
        {
            TimeSpan WindowTime { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly LaggyGridGpsCreator _gpsCreator;
        readonly PersistencyObserver<long> _reports;
        DateTime? _lastCollectionTimestamp;

        public LaggyGridReportBuffer(IConfig config, LaggyGridGpsCreator gpsCreator)
        {
            _config = config;
            _gpsCreator = gpsCreator;
            _reports = new PersistencyObserver<long>();
        }

        public void UpdateLaggyGrids(IEnumerable<LaggyGridReport> laggyGrids)
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
            var longLaggyGrids = longLaggyGridIds.Select(i => laggyGridsMap[i]);
            _gpsCreator.CreateGps(longLaggyGrids).Forget(Log);

            Log.Trace($"done updating collection: {laggyGrids.ToStringSeq()}");
        }

        public void Clear()
        {
            _reports.Clear();
        }
    }
}