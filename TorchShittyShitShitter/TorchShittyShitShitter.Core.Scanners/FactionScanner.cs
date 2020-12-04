using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Utils.General;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter.Core.Scanners
{
    public sealed class FactionScanner : ILagScanner
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ILagScannerConfig _config;
        readonly FactionMemberProfiler _factionMemberProfiler;
        readonly List<IMyFaction> _laggyFactions;

        public FactionScanner(ILagScannerConfig config, FactionMemberProfiler factionMemberProfiler)
        {
            _config = config;
            _factionMemberProfiler = factionMemberProfiler;
            _laggyFactions = new List<IMyFaction>();
        }

        public async Task LoopProfilingFactions(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                await ProfileFactions(canceller);
            }
        }

        async Task ProfileFactions(CancellationToken canceller)
        {
            var factions = await _factionMemberProfiler.Profile(10.Seconds(), canceller);

            lock (_laggyFactions)
            {
                _laggyFactions.Clear();

                foreach (var (faction, _, mspf) in factions)
                {
                    if (mspf > _config.MspfPerOnlineGroupMember)
                    {
                        _laggyFactions.Add(faction);
                    }
                }

                Log.Trace($"Laggy factions: {_laggyFactions.Select(f => f.Tag).ToStringSeq()}");
            }
        }

        public IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids)
        {
            IEnumerable<IMyFaction> factions;
            lock (_laggyFactions)
            {
                factions = _laggyFactions.ToArray();
            }

            // get a mapping from players to their laggy factions
            var factionMembers = new Dictionary<long, IMyFaction>(); // key is player id
            foreach (var faction in factions)
            foreach (var (_, factionMember) in faction.Members)
            {
                var playerId = factionMember.PlayerId;
                factionMembers[playerId] = faction;
            }

            // get the laggiest grid of laggy factions
            var topGrids = new Dictionary<IMyFaction, LaggyGridReport>();
            var remainingFactions = new HashSet<IMyFaction>(factions);
            foreach (var (grid, gridMspf) in profiledGrids)
            {
                // found all the laggiest grids
                if (!remainingFactions.Any()) break;

                foreach (var gridOwnerId in grid.BigOwners)
                {
                    if (factionMembers.TryGetValue(gridOwnerId, out var faction))
                    {
                        if (!topGrids.ContainsKey(faction))
                        {
                            var report = new LaggyGridReport(grid.EntityId, gridMspf, grid.DisplayName, factionTag: faction.Tag);
                            topGrids.Add(faction, report);
                            remainingFactions.Remove(faction);
                        }
                    }
                }
            }

            return topGrids.Values;
        }
    }
}