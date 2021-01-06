using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Broadcast GPS entities to online players.
    /// Clean up old GPS entities.
    /// </summary>
    public sealed class LaggyGridGpsBroadcaster
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
        readonly PersistentGpsHashStore _persistentGpsHashes;
        readonly DeprecationObserver<long> _gpsTimestamps;
        readonly EntityIdGpsCollection _gpsCollection;

        public LaggyGridGpsBroadcaster(IConfig config, PersistentGpsHashStore persistentGpsHashes)
        {
            _config = config;
            _persistentGpsHashes = persistentGpsHashes;
            _gpsTimestamps = new DeprecationObserver<long>();
            _gpsCollection = new EntityIdGpsCollection();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void BroadcastToOnlinePlayers(IEnumerable<MyGps> gpss)
        {
            var identityIds = GetDestinationIdentityIds().ToArray();

            foreach (var gps in gpss)
            {
                // Update this grid's last broadcast time
                _gpsTimestamps.Add(gps.EntityId);

                // actually send GPS to players
                _gpsCollection.SendAddOrModifyGps(identityIds, gps);
            }

            SaveGpsHashesToDisk();

            Log.Debug($"Broadcasting to {identityIds.Length} players: {gpss.Select(g => $"\"{g.Name}\"")}");
        }

        IEnumerable<long> GetDestinationIdentityIds()
        {
            var targetPlayers = new List<long>();
            var mutedPlayerIds = new HashSet<ulong>(_config.MutedPlayers);
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                if (mutedPlayerIds.Contains(onlinePlayer.SteamId())) continue;
                if (_config.AdminsOnly && onlinePlayer.IsNormalPlayer()) continue;

                targetPlayers.Add(onlinePlayer.Identity.IdentityId);
            }

            return targetPlayers;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DeleteAllCustomGpss()
        {
            var removedGridIds = _gpsTimestamps.RemoveAll();
            _gpsCollection.SendDeleteGpss(removedGridIds);

            SaveGpsHashesToDisk();
        }

        public async Task LoopCleaning(CancellationToken canceller)
        {
            Log.Trace("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                DeleteExpiredGpss();
                await Task.Delay(5.Seconds(), canceller);
            }
        }

        void DeleteExpiredGpss()
        {
            var removedGridIds = _gpsTimestamps.RemoveDeprecated(_config.GpsLifespan);
            _gpsCollection.SendDeleteGpss(removedGridIds);

            SaveGpsHashesToDisk();

            if (removedGridIds.Any())
            {
                Log.Debug($"Cleaned grids gps: {removedGridIds.ToStringSeq()}");
            }
        }

        void SaveGpsHashesToDisk()
        {
            var allTrackedGpsHashes = _gpsCollection.GetAllTrackedGpsHashes();
            _persistentGpsHashes.UpdateTrackedGpsHashes(allTrackedGpsHashes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<MyGps> GetAllCustomGpsEntities()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }
    }
}