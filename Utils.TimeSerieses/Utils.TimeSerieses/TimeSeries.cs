using System;
using System.Collections;
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

        public Timestamped<T> this[int index] => new Timestamped<T>(_timestamps[index], _elements[index]);

        public int Count => _timestamps.Count;
        public TimeSpan Length => GetLength();

        public IEnumerator<Timestamped<T>> GetEnumerator()
        {
            return _timestamps.Zip(_elements, (t, e) => new Timestamped<T>(t, e)).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        TimeSpan GetLength()
        {
            if (_timestamps.Count < 2) return TimeSpan.Zero;
            var firstTimestamp = _timestamps[0];
            var lastTimestamp = _timestamps[_timestamps.Count - 1];

            return lastTimestamp - firstTimestamp;
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

        public void RemoveOlderThan(DateTime minTimestamp)
        {
            if (Count == 0) return;

            var newTimestamps = new List<DateTime>();
            foreach (var timestamp in _timestamps)
            {
                if (timestamp > minTimestamp)
                {
                    newTimestamps.Add(timestamp);
                }
            }

            _timestamps.Clear();
            _timestamps.AddRange(newTimestamps);

            var newElements = new List<T>();
            for (var i = 0; i < _timestamps.Count; i++)
            {
                var j = i + _elements.Count - _timestamps.Count;
                var e = _elements[j];
                newElements.Add(e);
            }

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