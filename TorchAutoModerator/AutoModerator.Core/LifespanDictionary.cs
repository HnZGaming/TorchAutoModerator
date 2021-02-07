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

        public bool ContainsKey(K key)
        {
            return _self.ContainsKey(key);
        }

        public bool TryGetRemainingTime(K key, out TimeSpan remainingTime)
        {
            if (_self.TryGetValue(key, out var endTime))
            {
                remainingTime = endTime - DateTime.UtcNow;
                return true;
            }

            remainingTime = default;
            return false;
        }

        public IEnumerable<(K Key, TimeSpan RemainingTime)> GetRemainingTimes()
        {
            foreach (var (k, endTime) in _self)
            {
                var remainingTime = endTime - DateTime.UtcNow;
                yield return (k, remainingTime);
            }
        }

        public IReadOnlyDictionary<K, TimeSpan> ToDictionary()
        {
            return GetRemainingTimes().ToDictionary();
        }

        public void AddOrUpdate(IEnumerable<K> keys, TimeSpan lifespan)
        {
            var startTime = DateTime.UtcNow;
            foreach (var key in keys)
            {
                _self[key] = startTime + lifespan;
            }
        }

        public void RemoveExpired()
        {
            foreach (var (key, endTime) in _self.ToArray())
            {
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