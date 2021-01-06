using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace EntityGpsBroadcasters.Core
{
    /// <summary>
    /// Save GPS hashes to the disk.
    /// Intended to properly delete GPS entities that
    /// accidentally got carried over from the next session.
    /// </summary>
    internal sealed class HashCollectionFile
    {
        readonly string _filePath;

        public HashCollectionFile(string filePath)
        {
            _filePath = filePath;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void UpdateHashCollection(IEnumerable<int> hashes)
        {
            var lines = new List<string>();
            foreach (var gpsHash in hashes)
            {
                lines.Add($"{gpsHash}");
            }

            File.WriteAllLines(_filePath, lines);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<int> GetHashCollection()
        {
            if (!File.Exists(_filePath)) return Enumerable.Empty<int>();

            var lines = File.ReadAllLines(_filePath);
            var hashes = new HashSet<int>();
            foreach (var line in lines)
            {
                if (int.TryParse(line, out var hash))
                {
                    hashes.Add(hash);
                }
            }

            return hashes;
        }
    }
}