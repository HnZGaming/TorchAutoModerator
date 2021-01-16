using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NLog;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Core
{
    internal sealed class EntityLagTimeSeries
    {
        public interface IConfig
        {
            double LongLaggyWindow { get; }
            double ProfileResultsExpireTime { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly TaggedTimeSeries<long, double> _taggedTimeSeries;

        public EntityLagTimeSeries(IConfig config)
        {
            _config = config;
            _taggedTimeSeries = new TaggedTimeSeries<long, double>();
        }

        public IEnumerable<long> GridIds => _taggedTimeSeries.Tags;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _taggedTimeSeries.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<(long EntityId, double ProfiledNormal)> profileResults)
        {
            var timestamp = DateTime.UtcNow;

            foreach (var (entityId, profiledNormal) in profileResults)
            {
                _taggedTimeSeries.AddPoint(entityId, timestamp, profiledNormal);
            }

            // append zero to time series that didn't have new input
            var profileResultMap = profileResults.ToDictionary(r => r.EntityId);
            foreach (var existingGridId in _taggedTimeSeries.Tags)
            {
                if (!profileResultMap.ContainsKey(existingGridId))
                {
                    _taggedTimeSeries.AddPoint(existingGridId, timestamp, 0);
                }
            }

            // keep the time series small
            var removeFrom = timestamp - _config.ProfileResultsExpireTime.Seconds();
            _taggedTimeSeries.RemovePointsOlderThan(removeFrom);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<long> GetLaggyGridIds()
        {
            foreach (var (gridId, timeSeries) in _taggedTimeSeries.GetAllTimeSeries())
            {
                if (IsLongLaggy(timeSeries))
                {
                    yield return gridId;
                }
            }
        }

        bool IsLongLaggy(ITimeSeries<double> timeSeries)
        {
            if (timeSeries.Count == 0) return false;

            var now = DateTime.UtcNow;
            var capTimestamp = now - _config.LongLaggyWindow.Seconds();

            var sumNormal = 0d;
            var sumCount = 0;
            for (var i = timeSeries.Count - 1; i >= 0; i--)
            {
                var (timestamp, normal) = timeSeries.GetPointAt(i);
                if (timestamp < capTimestamp) break;

                sumNormal += normal;
                sumCount += 1;
            }

            var avgNormal = sumNormal / sumCount;
            return avgNormal >= 1f;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public string ToString(long gridId, string timestampFormat, int width)
        {
            if (!_taggedTimeSeries.TryGetTimeSeries(gridId, out var timeSeries))
            {
                throw new Exception($"time series not found: {gridId}");
            }

            var sb = new StringBuilder();
            for (var i = 0; i < timeSeries.Count; i++)
            {
                var (timestamp, lag) = timeSeries.GetPointAt(i);
                sb.Append(timestamp.ToString(timestampFormat));
                sb.Append(' ');

                var normal = Math.Min(1, lag);
                var starIndex = (int) (width * normal);
                for (var j = 0; j < width; j++)
                {
                    var c = j == starIndex ? '+' : '-';
                    sb.Append(c);
                }

                sb.Append(' ');
                sb.Append($"{lag * 100:0}%");
            }

            return sb.ToString();
        }
    }
}