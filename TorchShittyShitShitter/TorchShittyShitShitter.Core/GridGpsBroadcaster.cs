using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using TorchShittyShitShitter.Reflections;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Broadcast GPS entities to online players.
    /// Clean up old GPS entities.
    /// </summary>
    public sealed class GridGpsBroadcaster
    {
        public interface IConfig
        {
            /// <summary>
            /// Length of time to keep no-longer-laggy grids in everyone's HUD.
            /// </summary>
            TimeSpan GpsLifespan { get; }

            /// <summary>
            /// Steam IDs of players who have muted this GPS broadcaster.
            /// </summary>
            IEnumerable<ulong> MutedPlayers { get; }

            /// <summary>
            /// Broadcast to admin players only.
            /// </summary>
            bool AdminsOnly { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly DeprecationObserver<long> _gpsTimestamps;

        // Map from a grid entity ID to the hash of a GPS entity that's tracking the grid.
        // Trying to use a grid entity ID as a key of a GPS entity
        // because the hash is useless (bc GPS name is taken into the hash function).
        readonly Dictionary<long, int> _gridIdGpsHashMap;

        public GridGpsBroadcaster(IConfig config)
        {
            _config = config;
            _gpsTimestamps = new DeprecationObserver<long>();
            _gridIdGpsHashMap = new Dictionary<long, int>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        public void BroadcastToOnlinePlayers(MyGps gps)
        {
            gps.ThrowIfNull(nameof(gps));

            // Update this grid's last broadcast time
            _gpsTimestamps.Add(gps.EntityId);

            // Delete GPS entities with the same entity ID
            // but keep the list of their identity IDs -> A.
            // Send new GPS entities where, 
            // if a receiver's identity ID is included in A, don't make a ping sound.

            var deletedGpss = GpsCollection.Where(g =>
                _gridIdGpsHashMap.ContainsKey(g.EntityId) &&
                g.EntityId == gps.EntityId);

            var oldIdentityIds = new HashSet<long>();

            foreach (var (identityId, deletedGps) in deletedGpss)
            {
                GpsCollection.SendDelete(identityId, deletedGps.Hash);
                oldIdentityIds.Add(identityId);
            }

            var newIdentityIds = GetDestinationIdentityIds().ToArray();
            foreach (var identityId in newIdentityIds)
            {
                var playSound = !oldIdentityIds.Contains(identityId);
                GpsCollection.SendAddGps(identityId, gps, playSound);
            }

            _gridIdGpsHashMap[gps.EntityId] = gps.Hash;

            Log.Debug($"Broadcasting to {newIdentityIds.Length} players: \"{gps.Name}\" (id: {gps.EntityId})");
        }

        IEnumerable<long> GetDestinationIdentityIds()
        {
            var targetPlayers = new List<long>();
            var mutedPlayerIds = new HashSet<ulong>(_config.MutedPlayers);
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                if (mutedPlayerIds.Contains(onlinePlayer.SteamId())) continue;
                if (_config.AdminsOnly && !IsAdmin(onlinePlayer)) continue;

                targetPlayers.Add(onlinePlayer.Identity.IdentityId);
            }

            return targetPlayers;
        }

        static bool IsAdmin(IMyPlayer onlinePlayer)
        {
            return onlinePlayer.PromoteLevel >= MyPromoteLevel.Moderator;
        }

        public void DeleteAllCustomGps()
        {
            // wipe all from the tracker
            var removedGridIds = _gpsTimestamps.RemoveAll();
            _gridIdGpsHashMap.Clear();

            // delete all custom gps from the world
            var removedGridIdSet = new HashSet<long>(removedGridIds);
            GpsCollection.DeleteWhere(gps => removedGridIdSet.Contains(gps.EntityId));
        }

        public async Task LoopCleaning(CancellationToken canceller)
        {
            Log.Trace("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                DeleteExpiredGps();
                await Task.Delay(5.Seconds(), canceller);
            }
        }

        void DeleteExpiredGps()
        {
            var removedGridIds = _gpsTimestamps.RemoveDeprecated(_config.GpsLifespan);
            var removedGridIdSet = new HashSet<long>(removedGridIds);
            GpsCollection.DeleteWhere(gps => removedGridIdSet.Contains(gps.EntityId));

            foreach (var removedGridId in removedGridIdSet)
            {
                _gridIdGpsHashMap.Remove(removedGridId);
            }

            if (removedGridIds.Any())
            {
                Log.Debug($"Cleaned grids gps: {removedGridIds.ToStringSeq()}");
            }
        }

        public IEnumerable<MyGps> GetAllCustomGpsEntities()
        {
            var customGpss = GpsCollection.Where(
                g => _gridIdGpsHashMap.ContainsKey(g.EntityId));

            var gpsMap = customGpss.ToDictionary(
                p => p.Gps.Hash, p => p.Gps);

            return gpsMap.Values;
        }
    }
}