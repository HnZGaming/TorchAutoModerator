using System.Collections.Generic;
using NLog;
using Sandbox.Game.World;
using Utils.Torch;

namespace AutoModerator.Core
{
    public sealed class TargetPlayerCollector
    {
        public interface IConfig
        {
            /// <summary>
            /// Steam IDs of players who have muted this GPS broadcaster.
            /// </summary>
            IEnumerable<ulong> MutedPlayers { get; }

            /// <summary>
            /// Broadcast to admin players only.
            /// </summary>
            bool AdminsOnly { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;

        public TargetPlayerCollector(IConfig config)
        {
            _config = config;
        }

        public IEnumerable<long> GetTargetPlayerIds()
        {
            var targetPlayers = new List<long>();
            var mutedPlayerIds = new HashSet<ulong>(_config.MutedPlayers);
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                if (mutedPlayerIds.Contains(onlinePlayer.SteamId())) continue;
                if (_config.AdminsOnly && !onlinePlayer.IsAdmin()) continue;

                targetPlayers.Add(onlinePlayer.Identity.IdentityId);
            }

            return targetPlayers;
        }
    }
}