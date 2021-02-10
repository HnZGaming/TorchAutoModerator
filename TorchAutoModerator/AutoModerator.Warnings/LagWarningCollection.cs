using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Warnings
{
    public sealed class LagWarningCollection
    {
        public interface IConfig
        {
            string WarningTitle { get; }
            string WarningDetailMustProfileSelfText { get; }
            string WarningDetailMustDelagSelfText { get; }
            string WarningDetailMustWaitUnpinnedText { get; }
            string WarningDetailEndedText { get; }
            string WarningCurrentLevelText { get; }
        }

        enum QuestState
        {
            Invalid,
            MustProfileSelf,
            MustDelagSelf,
            MustWaitUnpinned,
            Ended, // show players that the quest is done
            Cleared, // remove the hud
        }

        sealed class PlayerState
        {
            public QuestState Quest { get; set; }
            public LagWarningSource Latest { get; set; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly ConcurrentDictionary<long, PlayerState> _quests;
        readonly HudNotificationCollection _hudNotifications;

        public LagWarningCollection(IConfig config)
        {
            _config = config;
            _quests = new ConcurrentDictionary<long, PlayerState>();
            _hudNotifications = new HudNotificationCollection();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            foreach (var (playerId, _) in _quests)
            {
                UpdateQuestLog(QuestState.Cleared, playerId);
            }

            _quests.Clear();
            _hudNotifications.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear(long playerId)
        {
            _quests.Remove(playerId);
            UpdateQuestLog(QuestState.Cleared, playerId);
            _hudNotifications.Remove(playerId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<LagWarningSource> players)
        {
            var laggyPlayers = players
                .Where(p => p.LongLagNormal > 1 || p.IsPinned)
                .ToArray();

            foreach (var laggyPlayer in laggyPlayers)
            {
                var playerId = laggyPlayer.PlayerId;
                var lag = laggyPlayer.LongLagNormal;

                // new entry
                if (!_quests.TryGetValue(playerId, out var playerState))
                {
                    var newPlayerState = new PlayerState
                    {
                        Quest = QuestState.MustProfileSelf,
                        Latest = laggyPlayer,
                    };

                    _quests[playerId] = newPlayerState;
                    UpdateQuestLog(newPlayerState.Quest, playerId);

                    Log.Trace($"warning (new): \"{laggyPlayer.PlayerName}\" {lag * 100:0}%");
                    continue;
                }

                // update
                if (laggyPlayer.IsPinned && playerState.Quest < QuestState.MustWaitUnpinned)
                {
                    playerState.Quest = QuestState.MustWaitUnpinned;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (lag < 1f && playerState.Quest <= QuestState.MustDelagSelf)
                {
                    playerState.Quest = QuestState.Ended;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (lag >= 1f && playerState.Quest >= QuestState.Ended)
                {
                    playerState.Quest = QuestState.MustProfileSelf;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (!laggyPlayer.IsPinned && playerState.Quest == QuestState.MustWaitUnpinned)
                {
                    playerState.Quest = QuestState.Ended;
                    UpdateQuestLog(playerState.Quest, playerId);
                }

                _quests[playerId].Latest = laggyPlayer;

                var message = $"{_config.WarningCurrentLevelText}: {lag * 100:0}%";
                if (laggyPlayer.IsPinned)
                {
                    message += $" ({laggyPlayer.Pin.TotalSeconds:0} seconds left)";
                }

                _hudNotifications.Show(playerId, message);
            }

            // removed
            var latestLaggyPlayerIdSet = laggyPlayers.Select(s => s.PlayerId).ToSet();
            foreach (var (existingPlayerId, state) in _quests.ToArray())
            {
                if (state.Quest >= QuestState.Ended)
                {
                    _quests.Remove(existingPlayerId);
                    UpdateQuestLog(QuestState.Cleared, existingPlayerId);
                }
                else if (!latestLaggyPlayerIdSet.Contains(existingPlayerId)) // removed now
                {
                    state.Quest = QuestState.Ended;
                    UpdateQuestLog(state.Quest, existingPlayerId);
                    _hudNotifications.Remove(existingPlayerId);
                }
            }

            foreach (var (_, state) in _quests)
            {
                Log.Trace($"warning: {state.Latest} {state.Quest}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void OnSelfProfiled(long playerId)
        {
            if (_quests.TryGetValue(playerId, out var state))
            {
                var lagNormal = state.Latest.LongLagNormal;
                if (state.Quest <= QuestState.MustProfileSelf)
                {
                    state.Quest = lagNormal >= 1f
                        ? QuestState.MustDelagSelf
                        : QuestState.MustWaitUnpinned;
                    UpdateQuestLog(state.Quest, playerId);
                }
            }
        }

        void UpdateQuestLog(QuestState quest, long playerId)
        {
            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"{playerId}");
            Log.Trace($"updating quest log: {playerName}: {quest}");

            switch (quest)
            {
                case QuestState.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, _config.WarningTitle, playerId);
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustProfileSelfText, true, true, playerId);
                    return;
                }
                case QuestState.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustDelagSelfText, true, true, playerId);
                    return;
                }
                case QuestState.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustWaitUnpinnedText, true, true, playerId);
                    return;
                }
                case QuestState.Ended:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailEndedText, true, true, playerId);
                    return;
                }
                case QuestState.Cleared:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(false, "", playerId);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(quest), quest, null);
            }
        }
    }
}