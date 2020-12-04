using System;
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
    public sealed class LaggyGridGpsMaker
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly LaggyGridGpsDescriptionMaker _descriptionMaker;

        public LaggyGridGpsMaker(LaggyGridGpsDescriptionMaker descriptionMaker)
        {
            _descriptionMaker = descriptionMaker;
        }

        public bool TryMakeGps(LaggyGridReport report, int rank, out MyGps gps)
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
            var mspfRatio = $"{report.MspfRatio * 100:0}%";
            var name = $"{grid.DisplayName} ({mspfRatio})";

            gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = $"{gridId}",
                DisplayName = name,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = Color.Purple,
                description = _descriptionMaker.Make(report, rank),
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            return true;
        }
    }
}