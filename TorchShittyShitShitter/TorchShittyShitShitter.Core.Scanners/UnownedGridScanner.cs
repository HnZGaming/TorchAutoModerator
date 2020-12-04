using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game.Entities;
using Utils.General;

namespace TorchShittyShitShitter.Core.Scanners
{
    public sealed class UnownedGridScanner : ILagScanner
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ILagScannerConfig _config;

        public UnownedGridScanner(ILagScannerConfig config)
        {
            _config = config;
        }

        public IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids)
        {
            var reports = new List<LaggyGridReport>();

            foreach (var (grid, gridMspf) in profiledGrids)
            {
                if (!grid.BigOwners.Any()) // nobody owns this grid
                {
                    if (gridMspf > _config.MspfPerOnlineGroupMember)
                    {
                        var report = new LaggyGridReport(
                            grid.EntityId,
                            gridMspf,
                            gridMspf / _config.MspfPerOnlineGroupMember,
                            grid.DisplayName);

                        reports.Add(report);
                    }
                }
            }

            Log.Trace($"Laggy unowned grids: {reports.ToStringSeq()}");

            return reports;
        }
    }
}