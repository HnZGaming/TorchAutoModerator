using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;

namespace EntityGpsBroadcasters.Core
{
    /// <summary>
    /// Broadcast GPS entities to online players.
    /// Clean up old GPS entities.
    /// </summary>
    public sealed class EntityGpsBroadcaster
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly HashCollectionFile _storeFile;
        readonly EntityIdGpsCollection _gpsCollection;

        public EntityGpsBroadcaster(string storeFilePath)
        {
            _storeFile = new HashCollectionFile(storeFilePath);
            _gpsCollection = new EntityIdGpsCollection();
        }

        public void SendDeleteAllTrackedGpss()
        {
            var savedGpsHashes = _storeFile.GetHashCollection();
            var savedGpsHashSet = new HashSet<int>(savedGpsHashes);
            MySession.Static.Gpss.DeleteWhere((_, g) => savedGpsHashSet.Contains(g.Hash));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendAdd(IEnumerable<MyGps> gpss, IEnumerable<long> players)
        {
            foreach (var gps in gpss)
            {
                // actually send GPS to players
                _gpsCollection.SendAddOrModifyGps(players, gps);
            }

            SaveGpsHashes();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SendDelete(IEnumerable<long> entityIds)
        {
            _gpsCollection.SendDeleteGpss(entityIds);
            SaveGpsHashes();
        }

        void SaveGpsHashes()
        {
            var allTrackedGpsHashes = _gpsCollection.GetAllTrackedGpsHashes();
            _storeFile.UpdateHashCollection(allTrackedGpsHashes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<MyGps> GetAllTrackedGpss()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }
    }
}