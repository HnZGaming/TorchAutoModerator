using System.Collections.Generic;
using Sandbox.Game.Entities;

namespace AutoModerator.Core.Scanners
{
    /// <summary>
    /// Extract grids from profiler results to broadcast to online players.
    /// </summary>
    public interface ILagScanner
    {
        /// <summary>
        /// Retrieve all grids that should be broadcasted to players.
        /// </summary>
        /// <returns>`profiledGrids` is ordered from the laggiest grid to the least.</returns>
        /// <param name="profiledGrids">Profiler result of grids, ordered from the laggiest grid to the least.</param>
        IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids);
    }
}