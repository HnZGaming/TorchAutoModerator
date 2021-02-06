using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Utils.General;

namespace AutoModerator.Core
{
    public class LifespanDictionary<K>
    {
        readonly ConcurrentDictionary<K, DateTime> _self;

        public LifespanDictionary()
        {
            _self = new ConcurrentDictionary<K, DateTime>();
        }

        public IEnumerable<K> Keys => _self.Keys;
        public int Count => _self.Count;
        public TimeSpan Lifespan { private get; set; }

        public bool ContainsKey(K key)
        {
            return _self.ContainsKey(key);
        }

        public IEnumerable<(K Key, TimeSpan RemainingTime)> GetRemainingTimes()
        {
            foreach (var (k, startTime) in _self)
            {
                var remainingTime = startTime + Lifespan - DateTime.UtcNow;
                yield return (k, remainingTime);
            }
        }

        public IReadOnlyDictionary<K, TimeSpan> ToDictionary()
        {
            return GetRemainingTimes().ToDictionary();
        }

        public void AddOrUpdate(IEnumerable<K> keys)
        {
            var startTime = DateTime.UtcNow;
            foreach (var key in keys)
            {
                _self[key] = startTime;
            }
        }

        public void RemoveExpired()
        {
            foreach (var (key, startTime) in _self.ToArray())
            {
                var endTime = startTime + Lifespan;
                if (endTime < DateTime.UtcNow)
                {
                    _self.Remove(key);
                }
            }
        }

        public void Clear()
        {
            _self.Clear();
        }
    }
}