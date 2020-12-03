using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Utils.General;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Hold onto laggy grid reports (before submitting to the GPS broadcaster)
    /// until consistently laggy for a good length of time.
    /// </summary>
    public sealed class LaggyGridReportBuffer
    {
        public interface IConfig
        {
            TimeSpan WindowTime { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly PersistencyObserver<long> _reports;

        public LaggyGridReportBuffer(IConfig config)
        {
            _config = config;
            _reports = new PersistencyObserver<long>();
        }

        public void AddInterval(IEnumerable<LaggyGridReport> laggyGrids)
        {
            laggyGrids.ThrowIfNull(nameof(laggyGrids));

            _reports.AddInterval(laggyGrids.Select(r => r.GridId));

            // remove old intervals
            _reports.CapBufferSize(_config.WindowTime);
        }

        public IEnumerable<long> GetPersistentlyLaggyGridIds()
        {
            return _reports.GetPersistentIntervals();
        }

        public void Clear()
        {
            _reports.Clear();
        }
    }
}