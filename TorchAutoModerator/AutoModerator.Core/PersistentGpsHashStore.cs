using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using AutoModerator.Reflections;
using Sandbox.Game.World;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Save GPS hashes to the disk.
    /// Intended to properly delete GPS entities that
    /// accidentally got carried over from the next session.
    /// </summary>
    public sealed class PersistentGpsHashStore
    {
        readonly string _path;

        public PersistentGpsHashStore(string path)
        {
            _path = path;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void UpdateTrackedGpsHashes(IEnumerable<int> gpsHashes)
        {
            var lines = new List<string>();
            foreach (var gpsHash in gpsHashes)
            {
                lines.Add($"{gpsHash}");
            }

            File.WriteAllLines(_path, lines);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        IEnumerable<int> GetAllTrackedGpsHashes()
        {
            if (!File.Exists(_path)) return Enumerable.Empty<int>();

            var lines = File.ReadAllLines(_path);
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

        public void DeleteAllTrackedGpssFromGame()
        {
            var savedGpsHashes = GetAllTrackedGpsHashes();
            var savedGpsHashSet = new HashSet<int>(savedGpsHashes);
            MySession.Static.Gpss.DeleteWhere((_, g) => savedGpsHashSet.Contains(g.Hash));
        }
    }
}