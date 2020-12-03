using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;

namespace TorchShittyShitShitter.Core.Scanners
{
    public sealed class SinglePlayerScanner : ILagScanner
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ILagScannerConfig _config;

        public SinglePlayerScanner(ILagScannerConfig config)
        {
            _config = config;
        }

        public IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids)
        {
            // get a mapping from "single" players to their grids
            var playerGrids = new Dictionary<long, List<(MyCubeGrid Grid, double Mspf)>>();
            foreach (var (grid, mspf) in profiledGrids)
            {
                foreach (var ownerId in grid.BigOwners)
                {
                    // skip players in a faction
                    var faction = MySession.Static.Factions.GetPlayerFaction(ownerId);
                    if (faction != null) continue;

                    // get or add new collection
                    if (!playerGrids.TryGetValue(ownerId, out var grids))
                    {
                        grids = new List<(MyCubeGrid, double)>();
                        playerGrids[ownerId] = grids;
                    }

                    grids.Add((grid, mspf));
                }
            }

            var reports = new List<LaggyGridReport>();
            foreach (var (_, grids) in playerGrids)
            {
                if (!grids.Any()) continue; // player doesn't have a grid

                var playerMspf = grids.Sum(g => g.Mspf);
                if (playerMspf > _config.MspfPerOnlineGroupMember) // laggy single player!
                {
                    var (laggiestGrid, gridMspf) = grids[0];
                    var report = new LaggyGridReport(laggiestGrid.EntityId, gridMspf);
                    reports.Add(report);
                }
            }

            Log.Trace($"Laggy single player grids: {reports.ToStringSeq()}");

            return reports;
        }
    }
}