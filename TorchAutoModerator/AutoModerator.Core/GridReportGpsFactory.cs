using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Utils.General;
using Utils.Torch;
using VRage;
using VRage.Game;
using VRageMath;

namespace AutoModerator.Core
{
    /// <summary>
    /// Create GPS entities for laggy grids.
    /// </summary>
    public sealed class GridReportGpsFactory
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly GridReportDescriber _describer;

        public GridReportGpsFactory(GridReportDescriber describer)
        {
            _describer = describer;
        }

        public async Task<IEnumerable<MyGps>> CreateGpss(IEnumerable<GridReport> gridReports, CancellationToken canceller)
        {
            // MyGps can be created in the game loop only (idk why)
            await GameLoopObserver.MoveToGameLoop(canceller);

            // create GPS entities of laggy grids
            var gpsCollection = new List<MyGps>();
            foreach (var (gridReport, i) in gridReports.Select((r, i) => (r, i)))
            {
                if (TryCreateGps(gridReport, i + 1, out var gps))
                {
                    gpsCollection.Add(gps);
                }
            }

            await TaskUtils.MoveToThreadPool(canceller);
            return gpsCollection;
        }

        bool TryCreateGps(GridReport report, int rank, out MyGps gps)
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
            var mspfRatio = $"{report.ThresholdNormal * 100:0}%";
            var name = $"{grid.DisplayName} ({mspfRatio})";

            gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = name,
                DisplayName = name,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = Color.Purple,
                description = _describer.Describe(report, rank),
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            return true;
        }
    }
}