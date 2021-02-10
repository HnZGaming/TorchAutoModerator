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
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly TaggedTimeSeries<long, double> _taggedTimeSeries;

        public EntityLagTimeSeries()
        {
            _taggedTimeSeries = new TaggedTimeSeries<long, double>();
        }

        public IEnumerable<long> EntityIds => _taggedTimeSeries.Tags;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _taggedTimeSeries.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddInterval(IEnumerable<(long EntityId, double ProfiledNormal)> profileResults)
        {
            var now = DateTime.UtcNow;

            foreach (var (entityId, profiledNormal) in profileResults)
            {
                _taggedTimeSeries.AddPoint(entityId, now, profiledNormal);
            }

            // append zero to time series that didn't have new input
            var profileResultMap = profileResults.ToDictionary(r => r.EntityId);
            foreach (var existingGridId in _taggedTimeSeries.Tags)
            {
                if (!profileResultMap.ContainsKey(existingGridId))
                {
                    _taggedTimeSeries.AddPoint(existingGridId, now, 0);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemovePointsOlderThan(TimeSpan timeSpan)
        {
            _taggedTimeSeries.RemovePointsOlderThan(DateTime.UtcNow - timeSpan);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public double GetLatestLagNormal(long entityId)
        {
            if (_taggedTimeSeries.TryGetTimeSeries(entityId, out var timeSeries))
            {
                var lastNormal = timeSeries.GetPointAt(timeSeries.Count - 1).Element;
                return lastNormal;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long EntityId, double Normal)> GetLatestLagNormals()
        {
            var result = new List<(long, double)>();
            if (!_taggedTimeSeries.Tags.Any()) return result;

            foreach (var (entityId, timeSeries) in _taggedTimeSeries.GetAllTimeSeries())
            {
                var lastNormal = timeSeries.GetPointAt(timeSeries.Count - 1).Element;
                result.Add((entityId, lastNormal));
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long EntityId, double Normal)> GetLongLagNormals(double minNormal)
        {
            var normals = new List<(long, double)>();
            foreach (var (entityId, timeSeries) in _taggedTimeSeries.GetAllTimeSeries())
            {
                var lagNormal = CalcLongLagNormal(timeSeries);
                if (lagNormal > minNormal)
                {
                    normals.Add((entityId, lagNormal));
                }
            }

            return normals;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveInactiveSerieses()
        {
            var inactiveSeriesTags = new List<long>();
            foreach (var (entityId, timeSeries) in _taggedTimeSeries.GetAllTimeSeries())
            {
                if (timeSeries.IsAll(0d))
                {
                    inactiveSeriesTags.Add(entityId);
                }
            }

            foreach (var inactiveSeriesTag in inactiveSeriesTags)
            {
                _taggedTimeSeries.RemoveSeries(inactiveSeriesTag);
            }
        }

        public IReadOnlyDictionary<long, double> GetLongLagNormalDictionary(double minNormal)
        {
            return GetLongLagNormals(minNormal).ToDictionary();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetLongLagNormal(long entityId, out double normal)
        {
            if (_taggedTimeSeries.TryGetTimeSeries(entityId, out var timeSeries))
            {
                normal = CalcLongLagNormal(timeSeries);
                return true;
            }

            normal = 0d;
            return false;
        }

        double CalcLongLagNormal(ITimeSeries<double> timeSeries)
        {
            if (timeSeries.Count == 0) return 0;

            var sumNormal = 0d;
            var sumCount = 0;
            for (var i = timeSeries.Count - 1; i >= 0; i--)
            {
                // if you wanna do something more complex you might use the timestamp
                var (timestamp, normal) = timeSeries.GetPointAt(i);

                sumNormal += normal;
                sumCount += 1;
            }

            var avgNormal = sumNormal / sumCount;
            return avgNormal;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public string ToString(long entityId, string timestampFormat, int width)
        {
            if (!_taggedTimeSeries.TryGetTimeSeries(entityId, out var timeSeries))
            {
                throw new Exception($"time series not found: {entityId}");
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