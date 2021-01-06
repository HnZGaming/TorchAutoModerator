using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Core
{
    public sealed class FactionMemberProfiler
    {
        public async Task<IEnumerable<(IMyFaction Faction, int Count, double Mspf)>> Profile(
            TimeSpan profileTime, CancellationToken canceller = default)
        {
            var mask = new GameEntityMask(null, null, null);
            using (var factionProfiler = new FactionProfiler(mask))
            using (ProfilerResultQueue.Profile(factionProfiler))
            {
                factionProfiler.MarkStart();

                await Task.Delay(profileTime, canceller);

                // get the number of online members of all factions
                var onlineFactions = new Dictionary<IMyFaction, int>();
                var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
                foreach (var onlinePlayer in onlinePlayers)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(onlinePlayer.PlayerId());
                    if (faction == null) continue;
                    onlineFactions.Increment(faction);
                }

                var factions = new List<(IMyFaction, int, double)>();

                var result = factionProfiler.GetResult();
                foreach (var (faction, entity) in result.GetTopEntities())
                {
                    var onlineMemberCount = onlineFactions.TryGetValue(faction, out var c) ? c : 0;
                    var mspf = entity.MainThreadTime / result.TotalFrameCount;
                    var mspfPerOnlineMember = mspf / Math.Max(1, onlineMemberCount);
                    factions.Add((faction, onlineMemberCount, mspfPerOnlineMember));
                }

                return factions;
            }
        }
    }
}