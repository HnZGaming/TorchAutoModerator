using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.API.Managers;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Punishes
{
    public sealed class LagPunishChatFeed
    {
        public interface IConfig
        {
            string PunishReportChatFormat { get; }
        }

        readonly IConfig _config;
        readonly IChatManagerServer _chatManager;
        readonly HashSet<long> _pinnedPlayerIds;

        public LagPunishChatFeed(IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            _chatManager = chatManager;
            _pinnedPlayerIds = new HashSet<long>();
        }

        public void Clear()
        {
            _pinnedPlayerIds.Clear();
        }

        public async Task Update(IEnumerable<LagPunishChatSource> sources)
        {
            var pinnedSources = sources.Where(s => s.IsPinned).ToArray();

            var grids = new Dictionary<long, MyCubeGrid>();

            await GameLoopObserver.MoveToGameLoop();

            foreach (var src in pinnedSources)
            {
                var gridId = src.LaggiestGridId;
                if (VRageUtils.TryGetCubeGridById(gridId, out var grid))
                {
                    grids[gridId] = grid;
                }
            }

            await TaskUtils.MoveToThreadPool();

            foreach (var src in pinnedSources)
            {
                if (_pinnedPlayerIds.Contains(src.PlayerId)) continue;

                var playerName =
                    MySession.Static.Players.TryGetPlayerById(src.PlayerId, out var player)
                        ? player.DisplayName
                        : $"<{src.PlayerId}>";

                var factionTag =
                    MySession.Static.Factions.TryGetFactionByPlayerId(src.PlayerId, out var faction)
                        ? faction.Tag
                        : "<none>";

                var gridName =
                    grids.TryGetValue(src.LaggiestGridId, out var grid)
                        ? grid.DisplayName
                        : $"<{src.LaggiestGridId}>";

                var message = _config
                    .PunishReportChatFormat
                    .Replace("{player}", playerName)
                    .Replace("{faction}", factionTag)
                    .Replace("{grid}", gridName)
                    .Replace("{level}", $"{src.LongLagNormal * 100:0}%");


                _chatManager.SendMessage("L.A.G. Detector", 0, message);
            }

            _pinnedPlayerIds.Clear();
            _pinnedPlayerIds.UnionWith(pinnedSources.Select(s => s.PlayerId));
        }
    }
}