using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Scan laggy grids via game profiling.
    /// </summary>
    public sealed class LaggyGridScanner
    {
        public interface IConfig
        {
            double MspfPerFactionMemberLimit { get; }
            int MaxLaggyGridCountPerScan { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;

        public LaggyGridScanner(IConfig config)
        {
            _config = config;
        }

        public IEnumerable<LaggyGridReport> ScanLaggyGrids(CancellationToken canceller)
        {
            Log.Trace("Dewing it");

            var mask = new GameEntityMask(null, null, null);
            using (var factionProfiler = new FactionProfiler(mask))
            using (var gridProfiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(factionProfiler))
            using (ProfilerResultQueue.Profile(gridProfiler))
            {
                factionProfiler.MarkStart();
                gridProfiler.MarkStart();

                // profile the world for some time
                if (!canceller.WaitHandle.WaitOneSafe(5.Seconds())) return null;

                var profiledFactions = factionProfiler.GetResult();
                var profiledGrids = gridProfiler.GetResult();

                var scanStartTick = Stopwatch.GetTimestamp();

                // scan
                var laggyGrids = new List<LaggyGridReport>();
                laggyGrids.AddRange(ScanFactionGrids(profiledFactions, profiledGrids));
                laggyGrids.AddRange(ScanSinglePlayerGrids(profiledGrids));

                // pick top laggiest grids
                var topLaggyGrids = laggyGrids
                    .OrderByDescending(r => r.Mspf)
                    .Take(_config.MaxLaggyGridCountPerScan)
                    .ToArray();

                var scanTime = (Stopwatch.GetTimestamp() - scanStartTick) / 10000D;
                Log.Debug($"Done scanning; took {scanTime:0.00}ms");
                Log.Debug($"Laggy grids: {topLaggyGrids.ToStringSeq()}");

                Log.Trace("did it");
                return topLaggyGrids;
            }
        }

        IEnumerable<LaggyGridReport> ScanFactionGrids(
            BaseProfilerResult<IMyFaction> profiledFactions,
            BaseProfilerResult<MyCubeGrid> profiledGrids)
        {
            Log.Trace("Scanning online member counts");

            // get the number of online members of all factions
            var onlineFactions = new Dictionary<IMyFaction, int>();
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(onlinePlayer.PlayerId());
                if (faction == null) continue;
                onlineFactions.Increment(faction);
            }

            Log.Trace("Scanning laggy factions");

            // get "laggy factions" (=factions whose profiled sim impact exceeds the limit)
            var laggyFactions = new List<IMyFaction>();
            foreach (var (faction, pEntry) in profiledFactions.GetTopEntities())
            {
                var mspfPerFaction = pEntry.MainThreadTime / profiledFactions.TotalFrameCount;

                var onlineMemberCount = onlineFactions.TryGetValue(faction, out var c) ? c : 0;
                var mspfPerFactionMember = mspfPerFaction / Math.Max(1, onlineMemberCount);
                if (mspfPerFactionMember > _config.MspfPerFactionMemberLimit)
                {
                    laggyFactions.Add(faction);
                }
            }

            Log.Trace($"Laggy factions: {laggyFactions.Select(f => f.Tag).ToStringSeq()}");
            Log.Trace("Mapping players to factions");

            // get a mapping from players to their laggy factions
            var laggyFactionMembers = new Dictionary<long, IMyFaction>(); // key is player id
            foreach (var faction in laggyFactions)
            foreach (var (_, factionMember) in faction.Members)
            {
                var playerId = factionMember.PlayerId;
                laggyFactionMembers[playerId] = faction;
            }

            Log.Trace("Scanning laggiest grids for factions");

            // get the laggiest grid of laggy factions
            var laggiestFactionGrids = new Dictionary<IMyFaction, LaggyGridReport>();
            var remainingFactions = new HashSet<IMyFaction>(laggyFactions);
            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                // found all the laggiest grids
                if (!remainingFactions.Any()) break;

                var gridMspf = entity.MainThreadTime / profiledGrids.TotalFrameCount;

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

        IEnumerable<LaggyGridReport> ScanSinglePlayerGrids(BaseProfilerResult<MyCubeGrid> profiledGrids)
        {
            Log.Trace("Scanning laggiest grids for single players");

            // get a mapping from "single" players to their grids
            var singlePlayerGrids = new Dictionary<long, List<(MyCubeGrid Grid, ProfilerEntry Entry)>>();
            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                foreach (var gridOwnerId in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.GetPlayerFaction(gridOwnerId);
                    if (faction == null) // single player!
                    {
                        if (!singlePlayerGrids.TryGetValue(gridOwnerId, out var grids))
                        {
                            grids = new List<(MyCubeGrid, ProfilerEntry)>();
                            singlePlayerGrids[gridOwnerId] = grids;
                        }

                        grids.Add((grid, entity));
                    }
                }
            }

            var singlePlayerLaggyGrids = new List<LaggyGridReport>();
            foreach (var (_, grids) in singlePlayerGrids)
            {
                if (!grids.Any()) continue; // player doesn't have a grid

                var totalMspf = grids.Sum(g => g.Entry.MainThreadTime) / profiledGrids.TotalFrameCount;
                if (totalMspf > _config.MspfPerFactionMemberLimit) // laggy single player!
                {
                    var (laggiestGrid, e) = grids[0];
                    var gridMspf = e.MainThreadTime / profiledGrids.TotalFrameCount;
                    var report = new LaggyGridReport(laggiestGrid.EntityId, gridMspf);
                    singlePlayerLaggyGrids.Add(report);
                }
            }

            Log.Trace($"Laggy single player grids: {singlePlayerLaggyGrids.ToStringSeq()}");

            return singlePlayerLaggyGrids;
        }
    }
}