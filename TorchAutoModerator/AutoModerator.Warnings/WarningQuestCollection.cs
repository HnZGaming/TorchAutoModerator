﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using Utils.Torch;

namespace AutoModerator.Warnings
{
    public sealed class WarningQuestCollection
    {
        public interface IConfig
        {
            string WarningTitle { get; }
            string WarningDetailMustProfileSelf { get; }
            string WarningDetailMustDelagSelf { get; }
            string WarningDetailMustWaitUnpinned { get; }
            string WarningDetailEnded { get; }
            string WarningNotificationFormat { get; }
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
            public LaggyPlayerSnapshot Latest { get; set; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly ConcurrentDictionary<long, PlayerState> _quests;
        readonly Dictionary<long, int> _notificationIds;

        public WarningQuestCollection(IConfig config)
        {
            _config = config;
            _quests = new ConcurrentDictionary<long, PlayerState>();
            _notificationIds = new Dictionary<long, int>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            foreach (var (playerId, _) in _quests)
            {
                UpdateQuestLog(QuestState.Cleared, playerId);
            }

            _quests.Clear();
        }

        public void Clear(long playerId)
        {
            _quests.Remove(playerId);
            UpdateQuestLog(QuestState.Cleared, playerId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<LaggyPlayerSnapshot> laggyPlayers)
        {
            foreach (var laggyPlayer in laggyPlayers)
            {
                var playerId = laggyPlayer.PlayerId;

                // new entry
                if (!_quests.TryGetValue(playerId, out var playerState))
                {
                    var newPlayerState = new PlayerState
                    {
                        Quest = QuestState.MustProfileSelf,
                        Latest = laggyPlayer,
                    };

                    _quests[playerId] = newPlayerState;
                    UpdateQuestLog(QuestState.MustProfileSelf, playerId);

                    Log.Trace($"warning (new): \"{laggyPlayer.PlayerName}\" {laggyPlayer.LongLagNormal * 100:0}%");
                    continue;
                }

                // update
                if (laggyPlayer.LongLagNormal < 1f && playerState.Quest <= QuestState.MustDelagSelf)
                {
                    playerState.Quest = laggyPlayer.IsPinned
                        ? QuestState.MustWaitUnpinned
                        : QuestState.Ended;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (laggyPlayer.LongLagNormal >= 1f && playerState.Quest >= QuestState.MustWaitUnpinned)
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

                SendHudNotification(playerId, laggyPlayer.LongLagNormal);
            }

            var latestPlayerIdSet = new HashSet<long>(laggyPlayers.Select(s => s.PlayerId));
            foreach (var (playerId, state) in _quests.ToArray())
            {
                if (state.Quest == QuestState.Ended)
                {
                    _quests.Remove(playerId);
                    UpdateQuestLog(QuestState.Cleared, playerId);
                }

                if (!latestPlayerIdSet.Contains(playerId) && state.Quest < QuestState.Ended)
                {
                    state.Quest = QuestState.Ended;
                    UpdateQuestLog(state.Quest, playerId);
                }
            }

            foreach (var (_, state) in _quests)
            {
                Log.Trace($"warning: \"{state.Latest.PlayerName}\" {state.Latest.PlayerLagNormal * 100:0}% {state.Quest}");
            }
        }

        void SendHudNotification(long playerId, double lag)
        {
            if (_notificationIds.TryGetValue(playerId, out var nid))
            {
                MyVisualScriptLogicProvider.RemoveNotification(nid);
            }

            var message = _config.WarningNotificationFormat.Replace("{level}", $"{lag * 100:0}");
            nid = MyVisualScriptLogicProvider.AddNotification(message, "Red", playerId);

            _notificationIds[playerId] = nid;
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
            Log.Debug($"{playerName}: {quest}");

            switch (quest)
            {
                case QuestState.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, _config.WarningTitle, playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustProfileSelf, true, true, playerId);
                    return;
                }
                case QuestState.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustDelagSelf, true, true, playerId);
                    return;
                }
                case QuestState.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustWaitUnpinned, true, true, playerId);
                    return;
                }
                case QuestState.Ended:
                {
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailEnded, true, true, playerId);
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