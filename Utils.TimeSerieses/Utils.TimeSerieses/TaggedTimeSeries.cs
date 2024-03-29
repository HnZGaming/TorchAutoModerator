﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Utils.TimeSerieses
{
    public sealed class TaggedTimeSeries<T, E>
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly Dictionary<T, TimeSeries<E>> _timeSeriesMap;

        public TaggedTimeSeries()
        {
            _timeSeriesMap = new Dictionary<T, TimeSeries<E>>();
        }

        public IEnumerable<T> Tags => _timeSeriesMap.Keys;
        public int Count => _timeSeriesMap.Count;

        public IEnumerable<(T Key, ITimeSeries<E> TimeSeries)> GetAllTimeSeries()
        {
            return _timeSeriesMap.Select(m => (m.Key, (ITimeSeries<E>) m.Value)).ToArray();
        }

        public IReadOnlyDictionary<T, ITimeSeries<E>> ToDictionary()
        {
            return _timeSeriesMap
                .Select(m => (m.Key, (ITimeSeries<E>)m.Value))
                .ToDictionary(p => p.Key, p => p.Item2);
        }

        public bool TryGetTimeSeries(T tag, out ITimeSeries<E> timeSeries)
        {
            if (_timeSeriesMap.TryGetValue(tag, out var t))
            {
                timeSeries = t;
                return true;
            }

            timeSeries = null;
            return false;
        }

        public void AddPoint(T tag, DateTime timestamp, E element)
        {
            if (!_timeSeriesMap.TryGetValue(tag, out var timeSeries))
            {
                timeSeries = new TimeSeries<E>();
                _timeSeriesMap[tag] = timeSeries;
            }

            timeSeries.Add(timestamp, element);
        }

        public void RemovePointsOlderThan(DateTime thresholdTimestamp)
        {
            foreach (var p in _timeSeriesMap.ToArray())
            {
                var tag = p.Key;
                var timeSeries = p.Value;

                timeSeries.Retain(thresholdTimestamp);
                if (timeSeries.Count == 0)
                {
                    _timeSeriesMap.Remove(tag);
                }
            }
        }

        public void Clear()
        {
            foreach (var timeSeries in _timeSeriesMap.Values)
            {
                timeSeries.Clear();
            }

            _timeSeriesMap.Clear();
        }

        public void RemoveSeries(T key)
        {
            _timeSeriesMap.Remove(key);
        }

        public void RemoveWhere(Func<ITimeSeries<E>, bool> f)
        {
            foreach (var p in _timeSeriesMap.ToArray())
            {
                if (f(p.Value))
                {
                    _timeSeriesMap.Remove(p.Key);
                }
            }
        }

        public void RemoveSeriesRange(IEnumerable<T> keys)
        {
            foreach (var key in keys)
            {
                _timeSeriesMap.Remove(key);
            }
        }

        public bool ContainsKey(T key)
        {
            return _timeSeriesMap.ContainsKey(key);
        }
    }
}