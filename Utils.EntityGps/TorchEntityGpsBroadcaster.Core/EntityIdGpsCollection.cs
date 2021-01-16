using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Sandbox.Game.Screens.Helpers;

namespace TorchEntityGpsBroadcaster.Core
{
    /// <summary>
    /// Manage GPS collections using EntityId instead of GPS hashes.
    /// Intended to work around the issue that GPS hashes are calculated based on their name (GPS name).
    /// That way we can update the name of an existing GPS entity without ringing the "ping" sound every time.
    /// </summary>
    /// <remarks>
    /// One game entity can only be attached to one GPS entity using this class.
    /// All GPS objects must have a game entity attached to it otherwise this class will break. 
    /// </remarks>
    public sealed class EntityIdGpsCollection
    {
        readonly PrefixedGpsCollection _prefixedGpsCollection;
        readonly Dictionary<long, Dictionary<long, MyGps>> _trackedGpss;

        public EntityIdGpsCollection(string prefix)
        {
            _prefixedGpsCollection = new PrefixedGpsCollection(prefix);
            _trackedGpss = new Dictionary<long, Dictionary<long, MyGps>>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<long> GetAllTrackedIdentityIds()
        {
            return _trackedGpss.Keys.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<MyGps> GetAllTrackedGpss()
        {
            var mappedGpss = new Dictionary<int, MyGps>();
            foreach (var (_, gpss) in _trackedGpss)
            foreach (var (_, gps) in gpss)
            {
                mappedGpss[gps.Hash] = gps;
            }

            return mappedGpss.Values;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<(long IdentityId, MyGps Gps)> GetAllTrackedPairs()
        {
            var pairs = new List<(long, MyGps)>();
            foreach (var (identityId, gpss) in _trackedGpss)
            foreach (var (_, gps) in gpss)
            {
                pairs.Add((identityId, gps));
            }

            return pairs;
        }

        // call this at the beginning of new session otherwise GPSs from the last session will mess with us
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteUntrackedGpss()
        {
            foreach (var (identityId, existingGps) in _prefixedGpsCollection.GetAllGpss())
            {
                if (!_trackedGpss.TryGetValue(identityId, out var trackedPlayerGpss))
                {
                    _prefixedGpsCollection.SendDeleteGps(identityId, existingGps.Hash);
                    continue;
                }

                if (!trackedPlayerGpss.ContainsKey(existingGps.EntityId))
                {
                    _prefixedGpsCollection.SendDeleteGps(identityId, existingGps.Hash);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteAllTrackedGpss()
        {
            foreach (var (identityId, gpss) in _trackedGpss)
            foreach (var (_, gps) in gpss)
            {
                _prefixedGpsCollection.SendDeleteGps(identityId, gps.Hash);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendAddOrModifyGps(long identityId, MyGps gps)
        {
            if (gps.EntityId == 0)
            {
                throw new Exception("GPS without game entity attached");
            }

            // delete existing GPS entities with the same entity IDs
            var existingGpss = _prefixedGpsCollection.GetPlayerGpss(identityId);
            var gpsExisted = false;
            foreach (var existingGps in existingGpss)
            {
                if (existingGps.EntityId == gps.EntityId)
                {
                    _prefixedGpsCollection.SendDeleteGps(identityId, existingGps.Hash);
                    gpsExisted = true;
                }
            }

            _prefixedGpsCollection.SendAddGps(identityId, gps, !gpsExisted);

            if (!_trackedGpss.TryGetValue(identityId, out var trackedGpss))
            {
                trackedGpss = new Dictionary<long, MyGps>();
                _trackedGpss[identityId] = trackedGpss;
            }

            trackedGpss[gps.EntityId] = gps;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteGps(long identityId, long entityId)
        {
            if (_trackedGpss.TryGetValue(identityId, out var gpss))
            {
                if (gpss.TryGetValue(entityId, out var gps))
                {
                    _prefixedGpsCollection.SendDeleteGps(identityId, gps.Hash);
                    _trackedGpss[identityId].Remove(entityId);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteGpss(IEnumerable<long> entityIds)
        {
            foreach (var identityId in GetAllTrackedIdentityIds())
            foreach (var entityId in entityIds)
            {
                SendDeleteGps(identityId, entityId);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendReplaceAllTrackedGpss(IEnumerable<MyGps> newGpss, IEnumerable<long> targetIds)
        {
            // delete existing GPSs whose entity ID is not listed in `gpss`
            var newGpsEntityIds = new HashSet<long>(newGpss.Select(g => g.EntityId));
            foreach (var (identityId, gps) in GetAllTrackedPairs())
            {
                if (!newGpsEntityIds.Contains(gps.EntityId))
                {
                    SendDeleteGps(identityId, gps.Hash);
                }
            }

            // add/modify other existing GPSs
            foreach (var gps in newGpss)
            foreach (var targetIdentityId in targetIds)
            {
                SendAddOrModifyGps(targetIdentityId, gps);
            }
        }
    }
}