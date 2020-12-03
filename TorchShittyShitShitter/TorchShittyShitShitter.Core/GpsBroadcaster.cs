using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using TorchShittyShitShitter.Reflections;
using Utils.General;
using Utils.Torch;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Broadcast GPS entities to online players.
    /// Clean up old GPS entities.
    /// Use MyGps.Name property to find and delete its own GPS entities from last sessions.
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
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly string _customName;
        readonly IConfig _config;
        readonly DeprecationObserver<int> _gpsTimestamps;

        public GpsBroadcaster(IConfig config, string customName)
        {
            _customName = customName;
            _config = config;
            _gpsTimestamps = new DeprecationObserver<int>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        public void BroadcastToOnlinePlayers(MyGps gps)
        {
            gps.ThrowIfNull(nameof(gps));

            // mark
            gps.Name = _customName;

            // Update this grid's last broadcast time
            _gpsTimestamps.Add(gps.Hash);

            var playerIds = GetDestinationIdentityIds().ToArray();
            GpsCollection.SendAddOrModify(playerIds, gps, gps.EntityId);

            Log.Debug($"Grid broadcast (to {playerIds.Length} players): {gps.EntityId} \"{gps.Name}\" \"{gps.DisplayName}\"");
        }

        IEnumerable<long> GetDestinationIdentityIds()
        {
            var targetPlayers = new List<MyPlayer>();
            var mutedPlayerIds = new HashSet<ulong>(_config.MutedPlayers);
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                if (!mutedPlayerIds.Contains(onlinePlayer.SteamId()))
                {
                    targetPlayers.Add(onlinePlayer);
                }
            }

            return targetPlayers.Select(p => p.Identity.IdentityId);
        }

        public void CleanAllCustomGps()
        {
            // delete all custom gps here
            GpsCollection.DeleteWhere(gps => gps.Name == _customName);

            // wipe the tracker too
            _gpsTimestamps.RemoveAll();
        }

        public void LoopCleaning(CancellationToken canceller)
        {
            Log.Trace("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                CleanOldGps();
                canceller.WaitHandle.WaitOneSafe(TimeSpan.FromSeconds(5));
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

        public IEnumerable<(long, MyGps)> GetAllCustomGpsEntities()
        {
            var allGpsCollection = GpsCollection.GetPlayerGpss();
            foreach (var (identityId, playerGpsCollection) in allGpsCollection)
            foreach (var (_, gps) in playerGpsCollection)
            {
                if (gps.Name == _customName)
                {
                    yield return (identityId, gps);
                }
            }
        }
    }
}