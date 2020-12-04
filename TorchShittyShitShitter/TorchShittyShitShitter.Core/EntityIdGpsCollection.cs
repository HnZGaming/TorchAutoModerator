using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using TorchShittyShitShitter.Reflections;
using Utils.Torch;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Manage GPS collections using their EntityId.
    /// Intended to properly identify GPS entities whose
    /// hash is not useful (eg. when their name has to change).
    /// </summary>
    public sealed class EntityIdGpsCollection
    {
        readonly Dictionary<long, int> _gridIdToGpsHashMap;

        public EntityIdGpsCollection()
        {
            _gridIdToGpsHashMap = new Dictionary<long, int>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendAddOrModifyGps(IEnumerable<long> identityIds, MyGps gps)
        {
            var identityIdSet = new HashSet<long>(identityIds);

            var deletedGpss = GpsCollection.Where((i, g) =>
                identityIdSet.Contains(i) &&
                _gridIdToGpsHashMap.ContainsKey(g.EntityId) &&
                g.EntityId == gps.EntityId);

            var oldIdentityIds = new HashSet<long>();

            foreach (var (identityId, deletedGps) in deletedGpss)
            {
                GpsCollection.SendDelete(identityId, deletedGps.Hash);
                oldIdentityIds.Add(identityId);
            }

            foreach (var identityId in identityIdSet)
            {
                var playSound = !oldIdentityIds.Contains(identityId);
                GpsCollection.SendAddGps(identityId, gps, playSound);
            }

            _gridIdToGpsHashMap[gps.EntityId] = gps.Hash;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteGpss(IEnumerable<long> gridIds)
        {
            var gridIdSet = new HashSet<long>(gridIds);

            GpsCollection.DeleteWhere((_, g) =>
                gridIdSet.Contains(g.EntityId) &&
                _gridIdToGpsHashMap.ContainsKey(g.EntityId));

            foreach (var gridId in gridIds)
            {
                _gridIdToGpsHashMap.Remove(gridId);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<int> GetAllTrackedGpsHashes()
        {
            return _gridIdToGpsHashMap.Values;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<MyGps> GetAllTrackedGpss()
        {
            var trackedGpsHashSet = new HashSet<int>(_gridIdToGpsHashMap.Values);

            var trackedGpss = GpsCollection.Where((_, gps) =>
                trackedGpsHashSet.Contains(gps.Hash));

            foreach (var (_, gps) in trackedGpss)
            {
                yield return gps;
            }
        }
    }
}