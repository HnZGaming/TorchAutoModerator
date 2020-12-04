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
    public sealed class GpsBroadcaster
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
        readonly DeprecationObserver<int> _gpsTimestamps;

        public GpsBroadcaster(IConfig config)
        {
            _config = config;
            _gpsTimestamps = new DeprecationObserver<int>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        public void BroadcastToOnlinePlayers(MyGps gps)
        {
            gps.ThrowIfNull(nameof(gps));

            // Update this grid's last broadcast time
            _gpsTimestamps.Add(gps.Hash);

            var playerIds = GetDestinationIdentityIds().ToArray();
            GpsCollection.SendAddOrModify(playerIds, gps, gps.EntityId);

            Log.Debug($"Broadcasting to {playerIds.Length} players: \"{gps.Name}\" (id: {gps.EntityId})");
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

        public void CleanAllCustomGps()
        {
            // wipe all from the tracker
            var removedGpsHashes = _gpsTimestamps.RemoveAll();

            // delete all custom gps from the world
            var removedGpsHashSet = new HashSet<int>(removedGpsHashes);
            GpsCollection.DeleteWhere(gps => removedGpsHashSet.Contains(gps.Hash));
        }

        public async Task LoopCleaning(CancellationToken canceller)
        {
            Log.Trace("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                CleanOldGps();
                await Task.Delay(5.Seconds(), canceller);
            }
        }

        void CleanOldGps()
        {
            var removedGpsHashes = _gpsTimestamps.RemoveDeprecated(_config.GpsLifespan);
            var removedGpsHashSet = new HashSet<int>(removedGpsHashes);
            GpsCollection.DeleteWhere(gps => removedGpsHashSet.Contains(gps.Hash));

            if (removedGpsHashes.Any())
            {
                Log.Debug($"Cleaned grids gps: {removedGpsHashes.ToStringSeq()}");
            }
        }

        public IEnumerable<MyGps> GetAllCustomGpsEntities()
        {
            var keyedCustomGpsCollection = new Dictionary<int, MyGps>();
            var allGpsCollection = GpsCollection.GetPlayerGpss();
            foreach (var (_, playerGpsCollection) in allGpsCollection)
            foreach (var (_, gps) in playerGpsCollection)
            {
                if (!keyedCustomGpsCollection.ContainsKey(gps.Hash))
                {
                    keyedCustomGpsCollection.Add(gps.Hash, gps);
                }
            }

            return keyedCustomGpsCollection.Values;
        }
    }
}