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

            // delete all the GPSs but remember the structure
            var existedGPSs = new Dictionary<long, HashSet<long>>();
            var existedEntityNames = new Dictionary<long, string>();
            foreach (var (identityId, gps) in _gpsCollection.GetAllGpss())
            {
                existedGPSs.Add(identityId, gps.EntityId);
                existedEntityNames[gps.EntityId] = gps.Name;
                _gpsCollection.SendDeleteGps(identityId, gps.Hash);
            }

            // add all new GPSs but don't make a ping sound if existed earlier
            foreach (var targetId in targetIds)
            foreach (var newGps in newGpss)
            {
                var existed = existedGPSs.Contains(targetId, newGps.EntityId);
                _gpsCollection.SendAddGps(targetId, newGps, !existed);

                if (!existed)
                {
                    Log.Info($"New GPS: {newGps.Name}");
                }

                existedGPSs.Remove(targetId, newGps.EntityId);
            }

            var removedEntityIds = new HashSet<long>();
            foreach (var (_, entityIds) in existedGPSs)
            foreach (var entityId in entityIds)
            {
                if (!removedEntityIds.Contains(entityId))
                {
                    removedEntityIds.Add(entityId);

                    var name = existedEntityNames.TryGetValue(entityId, out var n) ? $"\"{n}\"" : $"<{entityId}>";
                    Log.Info($"Deleted GPS: {name}");
                }
            }

            Log.Debug($"broadcasted {newGpss.Count()} laggy entities");
        }
    }
}