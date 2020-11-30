using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace TorchShittyShitShitter.Core
{
    /// <summary>
    /// Collect laggy grids via game profiling.
    /// </summary>
    public sealed class LaggyGridCollector
    {
        public interface IConfig
        {
            double MspfPerFactionMemberLimit { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;
        readonly LaggyGridWindowBuffer _gridBuffer;

        public LaggyGridCollector(
            IConfig config,
            LaggyGridWindowBuffer gridBuffer)
        {
            _config = config;
            _gridBuffer = gridBuffer;
        }

        public void CollectLaggyGrids(CancellationToken canceller)
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
                try
                {
                    canceller.WaitHandle.WaitOne(5.Seconds());
                }
                catch // on cancellation
                {
                    return;
                }

                CollectLaggyGrids(factionProfiler, gridProfiler);
            }
        }

        void CollectLaggyGrids(FactionProfiler factionProfiler, GridProfiler gridProfiler)
        {
            var profiledFactions = factionProfiler.GetResult();
            var profiledGrids = gridProfiler.GetResult();

            // final product
            var laggyGrids = new List<LaggyGridReport>();

            Log.Trace("Collecting...");

            var collectionStartTick = Stopwatch.GetTimestamp();

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
                var mspfPerFaction = pEntry.MainThreadTime / profiledFactions.TotalFrameCount;

                var onlineMemberCount = onlineFactions.TryGetValue(faction, out var c) ? c : 0;
                var mspfPerFactionMember = mspfPerFaction / Math.Max(1, onlineMemberCount);

                var limitRatio = mspfPerFactionMember / _config.MspfPerFactionMemberLimit;
                if (limitRatio > 1d)
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
            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                var gridMspf = entity.MainThreadTime / profiledGrids.TotalFrameCount;

                foreach (var gridOwnerId in grid.BigOwners)
                {
                    if (laggyFactionMembers.TryGetValue(gridOwnerId, out var laggyFaction))
                    {
                        if (!laggiestFactionGrids.ContainsKey(laggyFaction))
                        {
                            var report = new LaggyGridReport(grid.EntityId, gridMspf);
                            laggiestFactionGrids.Add(laggyFaction, report);
                        }
                    }
                }
            }

            // get a mapping from "single" players to their grids
            var singlePlayerGrids = new Dictionary<long, LaggyGridReport>();
            foreach (var (grid, entity) in profiledGrids.GetTopEntities())
            {
                var gridMspf = entity.MainThreadTime / profiledGrids.TotalFrameCount;

                foreach (var gridOwnerId in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.GetPlayerFaction(gridOwnerId);
                    if (faction == null) // single player!
                    {
                        if (!singlePlayerGrids.ContainsKey(gridOwnerId))
                        {
                            var gridReport = new LaggyGridReport(grid.EntityId, gridMspf);
                            singlePlayerGrids[gridOwnerId] = gridReport;
                        }
                    }
                }
            }

            Log.Trace($"Laggy single players: {singlePlayerGrids.Keys.ToStringSeq()}");

            laggyGrids.AddRange(laggiestFactionGrids.Values);
            laggyGrids.AddRange(singlePlayerGrids.Values);

            var collectionTotalTick = Stopwatch.GetTimestamp() - collectionStartTick;
            var collectionTotalTime = collectionTotalTick / 10000D;
            Log.Trace($"Done collecting; took {collectionTotalTime:0.00}ms");

            Log.Trace($"Laggy grids: {laggyGrids.Select(s => $"{s.GridId} ({s.Mspf:0.00}ms/f)").ToStringSeq()}");

            _gridBuffer.UpdateCollection(laggyGrids);
        }
    }
}