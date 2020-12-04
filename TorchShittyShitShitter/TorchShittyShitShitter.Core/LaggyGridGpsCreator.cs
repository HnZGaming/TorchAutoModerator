using System;
using System.Linq;
using System.Threading;
using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using VRage;
using VRage.Game;
using VRageMath;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Create GPS entities for laggy grids.
    /// </summary>
    public sealed class LaggyGridGpsCreator
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public bool TryCreateGps(LaggyGridReport report, int rank, out MyGps gps)
        {
            // must be called in the game loop
            if (Thread.CurrentThread.ManagedThreadId !=
                MySandboxGame.Static.UpdateThread.ManagedThreadId)
            {
                throw new Exception("Can be called in the game loop only");
            }

            var gridId = report.GridId;

            Log.Trace($"laggy grid report to be broadcast: {gridId}");

            gps = null;

            // this method fails outside the game loop
            if (!MyEntityIdentifier.TryGetEntity(gridId, out var entity, true))
            {
                Log.Warn($"Grid not found by EntityId: {gridId}");
                return false;
            }

            if (entity.Closed)
            {
                Log.Warn($"Grid found but closed: {gridId}");
                return false;
            }

            var grid = (MyCubeGrid) entity;

            gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = grid.DisplayName,
                DisplayName = grid.DisplayName,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = Color.Purple,
                description = $"The {RankToString(rank)} laggiest faction. Get 'em!",
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            return true;
        }

        static string RankToString(int rank)
        {
            switch ($"{rank}".Last())
            {
                case '1': return $"{rank}st";
                case '2': return $"{rank}nd";
                case '3': return $"{rank}rd";
                default: return $"{rank}th";
            }
        }
    }
}