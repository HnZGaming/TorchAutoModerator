using System.Collections.Generic;
using System.Linq;
using NLog;
using Torch.API.Managers;
using Utils.Torch;

namespace AutoModerator.Punishes
{
    public sealed class PunishChatFeed
    {
        public interface IConfig
        {
            string PunishReportChatName { get; }
            string PunishReportChatFormat { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly IChatManagerServer _chatManager;
        readonly HashSet<long> _pinnedPlayerIds;

        public PunishChatFeed(IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            _chatManager = chatManager;
            _pinnedPlayerIds = new HashSet<long>();
        }

        public void Clear()
        {
            _pinnedPlayerIds.Clear();
        }

        public void Update(IEnumerable<PunishSource> sources)
        {
            var pinnedSources = sources.Where(s => s.IsPinned).ToArray();

            foreach (var src in pinnedSources)
            {
                if (_pinnedPlayerIds.Contains(src.PlayerId)) continue;

                var message = _config
                    .PunishReportChatFormat
                    .Replace("{player}", src.PlayerName)
                    .Replace("{faction}", src.FactionTag)
                    .Replace("{grid}", src.GridName)
                    .Replace("{level}", $"{src.LagNormal * 100:0}%");

                _chatManager.SendMessage(_config.PunishReportChatName, 0, message);
                Log.Debug($"punishment chat sent: {src}");
            }

            _pinnedPlayerIds.Clear();
            _pinnedPlayerIds.UnionWith(pinnedSources.Select(s => s.PlayerId));
        }
    }
}