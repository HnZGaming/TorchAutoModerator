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

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Broadcast GPS entities to online players.
    /// Clean up old GPS entities.
    /// Use MyGps.Name property to delete all its own GPS entities from the last session.
    /// </summary>
    public sealed class GpsBroadcaster
    {
        public interface IConfig
        {
            /// <summary>
            /// Length of time to keep no-longer-laggy grids in everyone's HUD.
            /// </summary>
            TimeSpan GpsLifespan { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly string _customName;
        readonly IConfig _config;
        readonly DeprecationObserver<int> _broadcastGridTimestamps;

        public GpsBroadcaster(IConfig config, string customName)
        {
            _customName = customName;
            _config = config;
            _broadcastGridTimestamps = new DeprecationObserver<int>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        public void BroadcastToOnlinePlayers(MyGps gps)
        {
            gps.ThrowIfNull(nameof(gps));

            // mark
            gps.Name = _customName;

            // Send GPS to all online players
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            var onlinePlayerIds = onlinePlayers.Select(p => p.Identity.IdentityId);
            GpsCollection.SendAddOrModify(onlinePlayerIds, gps, gps.EntityId);

            // Update this grid's last broadcast time
            _broadcastGridTimestamps.Add(gps.Hash);

            Log.Trace($"grid broadcast: {gps.EntityId}");
        }

        public void CleanAllCustomGps()
        {
            // delete all custom gps here
            GpsCollection.DeleteWhere(gps => gps.Name == _customName);

            // wipe the tracker too
            _broadcastGridTimestamps.RemoveAll();
        }

        public void LoopCleaning(CancellationToken canceller)
        {
            Log.Info("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                CleanOldGps();

                try
                {
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                }
                catch // on cancellation
                {
                    return;
                }
            }
        }

        void CleanOldGps()
        {
            var removedGpsHashes = _broadcastGridTimestamps.RemoveDeprecated(_config.GpsLifespan);
            var removedGpsHashSet = new HashSet<int>(removedGpsHashes);
            GpsCollection.DeleteWhere(gps => removedGpsHashSet.Contains(gps.Hash));

            if (removedGpsHashes.Any())
            {
                Log.Trace($"Cleaned gps: {removedGpsHashes.ToStringSeq()}");
            }
        }
    }
}