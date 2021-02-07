using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox.Game;

namespace AutoModerator.Quests
{
    public sealed class SelfModQuestCollection
    {
        enum QuestState
        {
            Invalid = -1,
            MustProfileSelf = 0,
            MustDelagSelf = 1,
            MustWaitUnpinned = 2,
            Ended = 3,
            Cleared = 4,
        }

        sealed class PlayerState
        {
            public QuestState QuestState { get; set; }
            public LaggyPlayerSnapshot LatestSnapshot { get; set; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ConcurrentDictionary<long, PlayerState> _quests;

        public SelfModQuestCollection()
        {
            _quests = new ConcurrentDictionary<long, PlayerState>();
        }

        public void Clear()
        {
            foreach (var (playerId, _) in _quests)
            {
                UpdateQuestLog(QuestState.Cleared, playerId);
            }

            _quests.Clear();
        }

        public void Update(IEnumerable<LaggyPlayerSnapshot> playerSnapshot)
        {
            foreach (var snapshot in playerSnapshot)
            {
                var playerId = snapshot.PlayerId;

                // new entry
                if (!_quests.TryGetValue(playerId, out var playerState))
                {
                    var newPlayerState = new PlayerState
                    {
                        QuestState = QuestState.MustProfileSelf,
                        LatestSnapshot = snapshot,
                    };

                    _quests[playerId] = newPlayerState;
                    UpdateQuestLog(newPlayerState.QuestState, playerId);
                    continue;
                }

                // update
                if (snapshot.LongLagNormal < 1f && playerState.QuestState <= QuestState.MustDelagSelf)
                {
                    playerState.QuestState = snapshot.IsPinned
                        ? QuestState.MustWaitUnpinned
                        : QuestState.Ended;
                    UpdateQuestLog(playerState.QuestState, playerId);
                }
                else if (snapshot.LongLagNormal >= 1f && playerState.QuestState >= QuestState.MustWaitUnpinned)
                {
                    playerState.QuestState = QuestState.MustProfileSelf;
                    UpdateQuestLog(playerState.QuestState, playerId);
                }
                else if (!snapshot.IsPinned && playerState.QuestState == QuestState.MustWaitUnpinned)
                {
                    playerState.QuestState = QuestState.Ended;
                    UpdateQuestLog(playerState.QuestState, playerId);
                }

                _quests[playerId].LatestSnapshot = snapshot;
            }

            var latestPlayerIds = new HashSet<long>(playerSnapshot.Select(s => s.PlayerId));
            foreach (var (playerId, playerState) in _quests.ToArray())
            {
                if (playerState.QuestState == QuestState.Ended)
                {
                    UpdateQuestLog(QuestState.Cleared, playerId);
                    _quests.Remove(playerId);
                }

                if (!latestPlayerIds.Contains(playerId) && playerState.QuestState < QuestState.Ended)
                {
                    playerState.QuestState = QuestState.Ended;
                    UpdateQuestLog(playerState.QuestState, playerId);
                }
            }
        }

        public void OnSelfProfiled(long playerId)
        {
            if (_quests.TryGetValue(playerId, out var playerState))
            {
                if (playerState.QuestState == QuestState.MustProfileSelf)
                {
                    playerState.QuestState = playerState.LatestSnapshot.LongLagNormal >= 1f
                        ? QuestState.MustDelagSelf
                        : QuestState.MustWaitUnpinned;
                    UpdateQuestLog(playerState.QuestState, playerId);
                }
            }
        }

        void UpdateQuestLog(QuestState questState, long playerId)
        {
            Log.Debug($"{playerId}: {questState}");

            switch (questState)
            {
                case QuestState.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, "your laggy!", playerId);
                    MyVisualScriptLogicProvider.AddQuestlogObjective("profile yourself", true, true, playerId);
                    return;
                }
                case QuestState.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.AddQuestlogObjective("reduce lag", true, true, playerId);
                    return;
                }
                case QuestState.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.AddQuestlogObjective("wait pin to go", true, true, playerId);
                    return;
                }
                case QuestState.Ended:
                {
                    MyVisualScriptLogicProvider.AddQuestlogObjective("done", true, true, playerId);
                    return;
                }
                case QuestState.Cleared:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(false, null, playerId);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(questState), questState, null);
            }
        }
    }
}