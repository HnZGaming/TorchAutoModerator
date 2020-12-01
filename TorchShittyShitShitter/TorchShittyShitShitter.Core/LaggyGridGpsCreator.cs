using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Utils.Torch;
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
        readonly GpsBroadcaster _gpsBroadcaster;

        public LaggyGridGpsCreator(GpsBroadcaster gpsBroadcaster)
        {
            _gpsBroadcaster = gpsBroadcaster;
        }

        public async Task CreateGps(IEnumerable<LaggyGridReport> gridReports)
        {
            var gpsList = await CreateGpsAsync(gridReports);
            foreach (var gps in gpsList)
            {
                _gpsBroadcaster.BroadcastToOnlinePlayers(gps);
            }
        }

        Task<IEnumerable<MyGps>> CreateGpsAsync(IEnumerable<LaggyGridReport> gridReports)
        {
            var taskSource = new TaskCompletionSource<IEnumerable<MyGps>>();

            // Do this in the game loop because
            // querying entities by IDs works in there only.
            GameLoopObserver.OnNextUpdate(() =>
            {
                try
                {
                    var gpsList = new List<MyGps>();
                    foreach (var gridReport in gridReports)
                    {
                        var gpsOrNull = CreateGpsOrNull(gridReport);
                        if (gpsOrNull is MyGps gps)
                        {
                            gpsList.Add(gps);
                        }
                    }

                    taskSource.TrySetResult(gpsList);
                }
                catch (Exception e)
                {
                    taskSource.TrySetException(e);
                }
            });

            return taskSource.Task;
        }

        // must be called in the game loop
        MyGps CreateGpsOrNull(LaggyGridReport gridReport)
        {
            Log.Trace($"laggy grid report to be broadcast: {gridReport}");

            // this method fails outside the game loop
            if (!MyEntityIdentifier.TryGetEntity(gridReport.GridId, out var entity, true))
            {
                Log.Warn($"Grid not found by EntityId: {gridReport}");
                return null;
            }

            if (entity.Closed)
            {
                Log.Info($"Grid found but closed: {gridReport}");
                return null;
            }

            var grid = (MyCubeGrid) entity;

            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                DisplayName = grid.DisplayName,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = Color.Purple,
                description = $"Reported by {nameof(LaggyGridGpsCreator)} with love",
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            Log.Info($"Laggy grid GPS created: \"{grid.DisplayName}\" ({gridReport.Mspf:0.00}ms/f)");

            return gps;
        }
    }
}