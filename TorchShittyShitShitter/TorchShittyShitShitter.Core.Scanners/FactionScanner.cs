using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter.Core.Scanners
{
    public sealed class FactionScanner : ILagScanner
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ILagScannerConfig _config;
        readonly List<IMyFaction> _laggyFactions;

        public FactionScanner(ILagScannerConfig config)
        {
            _config = config;
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
            var mask = new GameEntityMask(null, null, null);
            using (var factionProfiler = new FactionProfiler(mask))
            using (ProfilerResultQueue.Profile(factionProfiler))
            {
                factionProfiler.MarkStart();

                await canceller.Delay(10.Seconds());

                // get the number of online members of all factions
                var onlineFactions = new Dictionary<IMyFaction, int>();
                var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
                foreach (var onlinePlayer in onlinePlayers)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(onlinePlayer.PlayerId());
                    if (faction == null) continue;
                    onlineFactions.Increment(faction);
                }

                lock (_laggyFactions)
                {
                    _laggyFactions.Clear();

                    var result = factionProfiler.GetResult();
                    foreach (var (faction, entity) in result.GetTopEntities())
                    {
                        var onlineMemberCount = onlineFactions.TryGetValue(faction, out var c) ? c : 0;
                        var mspf = entity.MainThreadTime / result.TotalFrameCount;
                        var mspfPerOnlineMember = mspf / Math.Max(1, onlineMemberCount);
                        if (mspfPerOnlineMember > _config.MspfPerOnlineGroupMember)
                        {
                            _laggyFactions.Add(faction);
                        }
                    }

                    Log.Trace($"Laggy factions: {_laggyFactions.Select(f => f.Tag).ToStringSeq()}");
                }
            }
        }

        public IEnumerable<LaggyGridReport> Scan(IEnumerable<(MyCubeGrid Grid, double Mspf)> profiledGrids)
        {
            IEnumerable<IMyFaction> laggyFactions;
            lock (_laggyFactions)
            {
                laggyFactions = _laggyFactions.ToArray();
            }

            // get a mapping from players to their laggy factions
            var laggyFactionMembers = new Dictionary<long, IMyFaction>(); // key is player id
            foreach (var faction in laggyFactions)
            foreach (var (_, factionMember) in faction.Members)
            {
                var playerId = factionMember.PlayerId;
                laggyFactionMembers[playerId] = faction;
            }

            // get the laggiest grid of laggy factions
            var laggiestFactionGrids = new Dictionary<IMyFaction, LaggyGridReport>();
            var remainingFactions = new HashSet<IMyFaction>(laggyFactions);
            foreach (var (grid, gridMspf) in profiledGrids)
            {
                // found all the laggiest grids
                if (!remainingFactions.Any()) break;

                foreach (var gridOwnerId in grid.BigOwners)
                {
                    if (laggyFactionMembers.TryGetValue(gridOwnerId, out var laggyFaction))
                    {
                        if (!laggiestFactionGrids.ContainsKey(laggyFaction))
                        {
                            var report = new LaggyGridReport(grid.EntityId, gridMspf);
                            laggiestFactionGrids.Add(laggyFaction, report);
                            remainingFactions.Remove(laggyFaction);
                        }
                    }
                }
            }

            return laggiestFactionGrids.Values;
        }
    }
}