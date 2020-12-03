using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public async Task<IEnumerable<LaggyGridReport>> ScanLaggyGrids(CancellationToken canceller)
        {
            Log.Debug($"Scanning laggy grids (threshold: {_config.MspfPerFactionMemberLimit})...");

            var reports = new List<LaggyGridReport>();

            var mask = new GameEntityMask(null, null, null);
            using (var factionProfiler = new FactionProfiler(mask))
            using (var gridProfiler = new GridProfiler(mask))
            using (ProfilerResultQueue.Profile(factionProfiler))
            using (ProfilerResultQueue.Profile(gridProfiler))
            {
                factionProfiler.MarkStart();
                gridProfiler.MarkStart();

                // profile the world for some time
                await canceller.Delay(5.Seconds());

                var profiledFactions = factionProfiler.GetResult();
                var profiledGrids = gridProfiler.GetResult();

                var scanStartTick = Stopwatch.GetTimestamp();

                // scan
                reports.AddRange(ScanFactionGrids(profiledFactions, profiledGrids));
                reports.AddRange(ScanSinglePlayerGrids(profiledGrids));
                reports.AddRange(ScanUnownedGrids(profiledGrids));

                var scanTime = (Stopwatch.GetTimestamp() - scanStartTick) / 10000D;
                Log.Trace($"Done scanning ({scanTime:0.00}ms spent)");
            }

            // pick top laggiest grids
            var topReports = reports
                .FilterUniqueByKey(r => r.GridId)
                .OrderByDescending(r => r.Mspf)
                .Take(_config.MaxLaggyGridCountPerScan)
                .ToArray();

            Log.Debug($"Laggy grids: {topReports.ToStringSeq()}");
            return topReports;
        }

        IEnumerable<LaggyGridReport> ScanFactionGrids(
            BaseProfilerResult<IMyFaction> profiledFactions,
            BaseProfilerResult<MyCubeGrid> profiledGrids)
        {
            // get the number of online members of all factions
            var onlineFactions = new Dictionary<IMyFaction, int>();
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(onlinePlayer.PlayerId());
                if (faction == null) continue;
                onlineFactions.Increment(faction);
            }

            // get "laggy factions" (=factions whose profiled sim impact exceeds the limit)
            var laggyFactions = new List<IMyFaction>();
            foreach (var (faction, pEntry) in profiledFactions.GetTopEntities())
            {
                var onlineMemberCount = onlineFactions.TryGetValue(faction, out var c) ? c : 0;
                var mspf = pEntry.MainThreadTime / profiledFactions.TotalFrameCount;
                var mspfPerOnlineMember = mspf / Math.Max(1, onlineMemberCount);
                if (mspfPerOnlineMember > _config.MspfPerFactionMemberLimit)
                {
                    laggyFactions.Add(faction);
                }
            }

            Log.Trace($"Laggy factions: {laggyFactions.Select(f => f.Tag).ToStringSeq()}");

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
            // get a mapping from "single" players to their grids
            var playerGrids = new Dictionary<long, List<(MyCubeGrid Grid, ProfilerEntry Entry)>>();
            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                foreach (var ownerId in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.GetPlayerFaction(ownerId);
                    if (faction != null) continue; // not a single player grid

                    if (!playerGrids.TryGetValue(ownerId, out var grids))
                    {
                        grids = new List<(MyCubeGrid, ProfilerEntry)>();
                        playerGrids[ownerId] = grids;
                    }

                    grids.Add((grid, entity));
                }
            }

            var reports = new List<LaggyGridReport>();
            foreach (var (_, grids) in playerGrids)
            {
                if (!grids.Any()) continue; // player doesn't have a grid

                var playerMspf = grids.Sum(g => g.Entry.MainThreadTime) / profiledGrids.TotalFrameCount;
                if (playerMspf > _config.MspfPerFactionMemberLimit) // laggy single player!
                {
                    var (laggiestGrid, e) = grids[0];
                    var gridMspf = e.MainThreadTime / profiledGrids.TotalFrameCount;
                    var report = new LaggyGridReport(laggiestGrid.EntityId, gridMspf);
                    reports.Add(report);
                }
            }

            Log.Trace($"Laggy single player grids: {reports.ToStringSeq()}");

            return reports;
        }

        IEnumerable<LaggyGridReport> ScanUnownedGrids(BaseProfilerResult<MyCubeGrid> profiledGrids)
        {
            var reports = new List<LaggyGridReport>();

            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                if (!grid.BigOwners.Any()) // nobody owns this grid
                {
                    var gridMspf = entity.MainThreadTime / profiledGrids.TotalFrameCount;
                    if (gridMspf > _config.MspfPerFactionMemberLimit)
                    {
                        var report = new LaggyGridReport(grid.EntityId, gridMspf);
                        reports.Add(report);
                    }
                }
            }

            Log.Trace($"Laggy unowned grids: {reports.ToStringSeq()}");

            return reports;
        }
    }
}