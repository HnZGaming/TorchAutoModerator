using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game.World;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Punishes.Broadcasts
{
    public sealed class BroadcastListenerCollection
    {
        public interface IConfig
        {
            IEnumerable<ulong> GpsMutedPlayers { get; }
            MyPromoteLevel GpsVisiblePromoteLevel { get; }
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
                if (CheckReceive(onlinePlayer))
                {
                    yield return onlinePlayer;
                }
            }
        }

        public IEnumerable<long> GetReceiverIdentityIds()
        {
            return GetReceivers().Select(r => r.IdentityId);
        }

        public IEnumerable<ulong> GetReceiverSteamIds()
        {
            foreach (var player in GetReceivers())
            {
                var steamId = player.SteamUserId;
                if (steamId != 0)
                {
                    yield return steamId;
                }
            }
        }

        void UpdateCollection()
        {
            _mutedPlayerIds.Clear();
            _mutedPlayerIds.UnionWith(_config.GpsMutedPlayers);
        }

        bool CheckReceive(MyPlayer player)
        {
            if (_mutedPlayerIds.Contains(player.SteamId())) return false;
            if (((IMyPlayer) player).PromoteLevel < _config.GpsVisiblePromoteLevel) return false;
            return true;
        }
    }
}