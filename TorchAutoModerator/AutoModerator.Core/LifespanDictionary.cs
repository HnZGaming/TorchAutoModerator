using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AutoModerator.Core
{
    public class LifespanDictionary<K>
    {
        readonly IDictionary<K, DateTime> _self;

        public LifespanDictionary()
        {
            _self = new ConcurrentDictionary<K, DateTime>();
        }

        public IEnumerable<K> Keys => _self.Keys;
        public int Count => _self.Count;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddOrUpdate(IEnumerable<K> keys)
        {
            var startTime = DateTime.UtcNow;
            foreach (var key in keys)
            {
                _self[key] = startTime;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveExpired(TimeSpan lifespan)
        {
            foreach (var (key, startTime) in _self.ToArray())
            {
                var endTime = startTime + lifespan;
                if (endTime < DateTime.UtcNow)
                {
                    _self.Remove(key);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(K Key, TimeSpan RemainingTime)> GetRemainingTimes()
        {
            foreach (var (key, endTime) in _self)
            {
                var remainingTime = endTime - DateTime.UtcNow;
                yield return (key, remainingTime);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _self.Clear();
        }
    }
}