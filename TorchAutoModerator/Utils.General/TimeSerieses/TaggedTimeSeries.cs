using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Utils.General.TimeSerieses
{
    internal sealed class TaggedTimeSeries<T, E>
    {
        readonly Dictionary<T, TimeSeries<E>> _timeSeriesMap;

        public TaggedTimeSeries()
        {
            _timeSeriesMap = new Dictionary<T, TimeSeries<E>>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(T, ITimeSeries<E>)> GetTaggedTimeSeries()
        {
            return _timeSeriesMap.Select(p => (p.Key, (ITimeSeries<E>) p.Value)).ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(T tag, DateTime timestamp, E element)
        {
            if (!_timeSeriesMap.TryGetValue(tag, out var timeSeries))
            {
                timeSeries = new TimeSeries<E>();
                _timeSeriesMap[tag] = timeSeries;
            }

            timeSeries.Add(timestamp, element);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveOlderThan(DateTime thresholdTimestamp)
        {
            foreach (var (tag, timeSeries) in _timeSeriesMap.ToArray())
            {
                timeSeries.RemoveOlderThan(thresholdTimestamp);
                if (timeSeries.IsEmpty)
                {
                    _timeSeriesMap.Remove(tag);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            foreach (var (_, timeSeries) in _timeSeriesMap)
            {
                timeSeries.Clear();
            }

            _timeSeriesMap.Clear();
        }
    }
}