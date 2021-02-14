using System.Collections.Generic;

namespace TorchEntityGpsBroadcaster.Core
{
    internal static class Utils
    {
        public static void AddOrReplace<K0, K1, V, D>(this IDictionary<K0, D> self, K0 key0, K1 key1, V value) where D : IDictionary<K1, V>, new()
        {
            if (!self.TryGetValue(key0, out var values))
            {
                values = new D();
                self[key0] = values;
            }

            values[key1] = value;
        }

        public static void Add<K, V, C>(this IDictionary<K, C> self, K key, V value) where C : ICollection<V>, new()
        {
            if (!self.TryGetValue(key, out var values))
            {
                values = new C();
                self[key] = values;
            }

            values.Add(value);
        }

        public static bool Contains<K, V, C>(this IDictionary<K, C> self, K key, V value) where C : ICollection<V>
        {
            return self.TryGetValue(key, out var vs) && vs.Contains(value);
        }

        public static void Remove<K, V, C>(this IDictionary<K, C> self, K key, V value) where C : ICollection<V>
        {
            if (self.TryGetValue(key, out var values))
            {
                values.Remove(value);
                if (values.Count == 0)
                {
                    self.Remove(key);
                }
            }
        }
    }
}