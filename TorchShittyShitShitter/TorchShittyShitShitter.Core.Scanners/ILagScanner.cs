using System.Collections.Generic;
using Sandbox.Game.Entities;

namespace TorchShittyShitShitter.Core.Scanners
{
    public interface ILagScanner
    {
        IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids);
    }
}