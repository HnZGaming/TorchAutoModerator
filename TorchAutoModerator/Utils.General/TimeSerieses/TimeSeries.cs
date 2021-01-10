using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.General.TimeSerieses
{
    internal sealed class TimeSeries<T> : ITimeSeries<T>
    {
        readonly SortedList<DateTime, T> _timeline;

        public TimeSeries()
        {
            _timeline = new SortedList<DateTime, T>(TimestampComparer.Default);
        }

        public bool IsEmpty => _timeline.Count == 0;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(DateTime Timestamp, T Element)> GetSeries()
        {
            return _timeline.Select(p => (p.Key, p.Value));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(DateTime timestamp, T element)
        {
            _timeline[timestamp] = element;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveOlderThan(DateTime thresholdTimestamp)
        {
            if (!_timeline.Any()) return;

            foreach (var timestamp in _timeline.Keys.AsArray())
            {
                if (timestamp > thresholdTimestamp) break;

                _timeline.Remove(timestamp);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _timeline.Clear();
        }
    }
}