using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using TorchShittyShitShitter.Reflections;
using Utils.General;
using VRage.Game;
using VRageMath;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Broadcast laggy grids until no longer.
    /// </summary>
    public sealed class LaggyGridGpsBroadcaster
    {
        public interface IConfig
        {
            /// <summary>
            /// Length of time to keep no-longer-laggy grids in everyone's HUD.
            /// </summary>
            TimeSpan GpsLifespan { get; }
        }

        const string CustomGpsName = "LaggyGridGps";
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly ConcurrentDictionary<int, DateTime> _broadcastGridTimestamps;

        public LaggyGridGpsBroadcaster(IConfig config)
        {
            _config = config;
            _broadcastGridTimestamps = new ConcurrentDictionary<int, DateTime>();
        }

        static MyGpsCollection GpsCollection => MySession.Static.Gpss;

        public void CleanAllCustomGps()
        {
            GpsCollection.DeleteWhere(gps => gps.Name == CustomGpsName);
        }

        public void LoopCleaning(CancellationToken canceller)
        {
            Log.Info("Started cleaner loop");

            while (!canceller.IsCancellationRequested)
            {
                var removedGpsHashes = GetOldGpsHashes();
                var removedGpsHashSet = new HashSet<int>(removedGpsHashes);
                GpsCollection.DeleteWhere(gps => removedGpsHashSet.Contains(gps.Hash));

                Log.Trace($"Cleaned gps: {removedGpsHashes.ToStringSeq()}");

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

        IEnumerable<int> GetOldGpsHashes()
        {
            var removedGpsHashes = new List<int>();
            foreach (var (gpsHash, lastReportTimestamp) in _broadcastGridTimestamps)
            {
                var endTime = DateTime.UtcNow - _config.GpsLifespan;
                if (endTime < lastReportTimestamp)
                {
                    removedGpsHashes.Add(gpsHash);
                }
            }

            return removedGpsHashes;
        }

        public void BroadcastGrid(LaggyGridReport gridReport)
        {
            var gps = CreateGps(gridReport);

            // Send GPS to all online players
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            var onlinePlayerIds = onlinePlayers.Select(p => p.Identity.IdentityId);
            GpsCollection.SendAddOrModify(onlinePlayerIds, gps, gps.EntityId);

            // Update this grid's last broadcast time
            _broadcastGridTimestamps[gps.Hash] = DateTime.UtcNow;
        }

        MyGps CreateGps(LaggyGridReport gridReport)
        {
            var grid = (MyCubeGrid) MyEntities.GetEntityById(gridReport.GridId);

            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = CustomGpsName,
                DisplayName = grid.DisplayName,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = Color.Purple,
                description = $"Reported by {nameof(LaggyGridGpsBroadcaster)} with love",
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            Log.Info($"Laggy grid GPS created: \"{grid.DisplayName}\" ({gridReport.Mspf:0.00}ms/f)");

            return gps;
        }
    }
}