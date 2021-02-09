using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game.World;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Broadcasts
{
    public sealed class BroadcastListenerCollection
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
        readonly HashSet<ulong> _mutedPlayerIds;

        public BroadcastListenerCollection(IConfig config)
        {
            _config = config;
            _mutedPlayerIds = new HashSet<ulong>();
        }

        public IEnumerable<IMyPlayer> GetReceivers()
        {
            UpdateCollection();
            foreach (var onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
            {
                if (CheckReceiveInternal(onlinePlayer))
                {
                    yield return onlinePlayer;
                }
            }
        }

        public IEnumerable<long> GetReceiverIdentityIds()
        {
            return GetReceivers().Select(r => r.IdentityId);
        }

        public bool CheckReceive(MyPlayer player)
        {
            UpdateCollection();
            return CheckReceiveInternal(player);
        }

        void UpdateCollection()
        {
            _mutedPlayerIds.Clear();
            _mutedPlayerIds.UnionWith(_config.MutedPlayers);
        }

        bool CheckReceiveInternal(MyPlayer player)
        {
            if (_mutedPlayerIds.Contains(player.SteamId())) return false;
            if (_config.AdminsOnly && !player.IsAdmin()) return false;
            return true;
        }
    }
}