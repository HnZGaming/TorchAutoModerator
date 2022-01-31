using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game.World;
using Torch.API.Managers;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Quests
{
    public sealed class QuestTracker
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly QuestEntity.IConfig _config;
        readonly IChatManagerServer _chatManager;
        readonly ConcurrentDictionary<long, QuestEntity> _entities;
        readonly HashSet<long> _lastOnlinePlayerIds;

        public QuestTracker(QuestEntity.IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            _chatManager = chatManager;
            _entities = new ConcurrentDictionary<long, QuestEntity>();
            _lastOnlinePlayerIds = new HashSet<long>();
        }

        public IReadOnlyDictionary<long, QuestEntity> Entities => _entities;

        public bool TryFindQuestForEntity(long entityId, out QuestEntity questEntity)
        {
            foreach (var (_, entity) in _entities)
            {
                if (entity.EntityId == entityId)
                {
                    questEntity = entity;
                    return true;
                }
            }

            questEntity = default;
            return false;
        }

        public void Clear()
        {
            foreach (var (_, entity) in _entities)
            {
                entity.Clear();
            }

            _entities.Clear();
        }

        public void Remove(long playerId)
        {
            if (_entities.TryGetValue(playerId, out var entity))
            {
                _entities.Remove(playerId);
                entity.Clear();
            }
        }

        public void Update(IEnumerable<QuestSource> sources)
        {
            // clear quest log of just-logged-in players
            {
                var onlinePlayerIds = MySession.Static.Players.GetOnlinePlayers().Select(p => p.PlayerId()).ToSet();
                var newPlayerIds = new HashSet<long>();
                newPlayerIds.UnionWith(onlinePlayerIds);
                newPlayerIds.ExceptWith(_lastOnlinePlayerIds);
                foreach (var newPlayerId in newPlayerIds)
                {
                    if (_entities.ContainsKey(newPlayerId)) continue; // shouldn't happen

                    QuestEntity.Clear(newPlayerId);
                    Log.Info($"cleared quest for new player: {newPlayerId}");
                }

                _lastOnlinePlayerIds.Clear();
                _lastOnlinePlayerIds.UnionWith(onlinePlayerIds);
            }

            // remove quests ended during the last interval
            foreach (var (playerId, entity) in _entities.ToArray())
            {
                if (entity.Quest == Quest.Ended)
                {
                    entity.Clear();
                    _entities.Remove(playerId);
                    Log.Info($"quest closed: {entity}");
                }
            }

            // update entities
            foreach (var source in sources)
            {
                // new entry
                var playerId = source.PlayerId;
                if (!_entities.TryGetValue(playerId, out var entity))
                {
                    Log.Info($"new quest entry: \"{source.PlayerName}\" {source.LagNormal:0.00}ms/f, pin({source.Pin.TotalSeconds:0}secs)");
                    _entities[playerId] = entity = new QuestEntity(playerId, _config, _chatManager);
                }

                entity.Update(source);
            }

            // end quests of players that aren't laggy or pinned anymore
            var currentLaggyPlayerIds = sources.Select(s => s.PlayerId).ToHashSet();
            foreach (var (playerId, entity) in _entities)
            {
                if (currentLaggyPlayerIds.Contains(playerId))
                {
                    Log.Debug($"quest ongoing: {entity}");
                }
                else
                {
                    entity.End();
                    Log.Info($"quest ended: {entity}");
                    // note: don't clear the quest here
                }
            }
        }

        public void OnSelfProfiled(long playerId)
        {
            if (_entities.TryGetValue(playerId, out var entity))
            {
                entity.OnSelfProfiled();
            }
        }
    }
}