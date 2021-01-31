using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
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
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly PrefixedGpsCollection _gpsCollection;

        public EntityIdGpsCollection(string prefix)
        {
            _gpsCollection = new PrefixedGpsCollection(prefix);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<MyGps> GetAllTrackedGpss()
        {
            return _gpsCollection.GetAllGpss().Select(g => g.Gps);
        }

        // call this at the beginning of new session
        // otherwise GPSs from the last session will mess with us
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDeleteAllGpss()
        {
            _gpsCollection.SendDeleteAllGpss();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendReplaceAllTrackedGpss(IEnumerable<MyGps> newGpss, IEnumerable<long> targetIds)
        {
            Log.Trace("replacing all GPSs...");

            var newEntityIdSet = new HashSet<long>(newGpss.Select(g => g.EntityId));
            var targetIdSet = new HashSet<long>(targetIds);

            // delete all the GPSs but remember which ones are updated
            var updatedEntityIds = new Dictionary<long, HashSet<long>>();
            foreach (var (identityId, gps) in _gpsCollection.GetAllGpss())
            {
                _gpsCollection.SendDeleteGps(identityId, gps.Hash);

                var entityId = gps.EntityId;
                var updated = targetIdSet.Contains(identityId) && newEntityIdSet.Contains(entityId);
                if (updated)
                {
                    updatedEntityIds.Add(identityId, entityId);
                }

                Log.Trace($"deleted old gps {entityId} (to {identityId}) update: {updated}");
            }

            // add all new GPSs
            foreach (var targetId in targetIds)
            foreach (var newGps in newGpss)
            {
                var updated = updatedEntityIds.Contains(targetId, newGps.EntityId);
                _gpsCollection.SendAddGps(targetId, newGps, !updated);
            }

            Log.Trace("Done replacing all GPSs");
        }
    }
}