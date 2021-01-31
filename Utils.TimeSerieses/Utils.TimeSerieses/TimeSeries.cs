using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.TimeSerieses
{
    public sealed class TimeSeries<T> : ITimeSeries<T>
    {
        readonly List<DateTime> _timestamps;
        readonly List<T> _elements;

        public TimeSeries()
        {
            _timestamps = new List<DateTime>();
            _elements = new List<T>();
        }

        public int Count => _timestamps.Count;

        public Timestamped<T> GetPointAt(int index)
        {
            return new Timestamped<T>(_timestamps[index], _elements[index]);
        }

        public void Add(DateTime timestamp, T element)
        {
            if (_timestamps.Count > 0)
            {
                var lastTimestamp = _timestamps[_timestamps.Count - 1];
                if (lastTimestamp > timestamp)
                {
                    throw new Exception("timestamp older than existing timestamps");
                }
            }

            _timestamps.Add(timestamp);
            _elements.Add(element);
        }

        public void RemoveOlderThan(DateTime thresholdTimestamp)
        {
            if (Count == 0) return;

            var newTimestamps = _timestamps.SkipWhile(t => t < thresholdTimestamp).ToArray();
            var newElements = _elements.Skip(_timestamps.Count - newTimestamps.Length).ToArray();

            _timestamps.Clear();
            _timestamps.AddRange(newTimestamps);

            _elements.Clear();
            _elements.AddRange(newElements);
        }

        public void Clear()
        {
            _timestamps.Clear();
            _elements.Clear();
        }
    }
}