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
            double WarningLagNormal { get; }
            string WarningTitle { get; }
            string WarningDetailMustProfileSelfText { get; }
            string WarningDetailMustDelagSelfText { get; }
            string WarningDetailMustWaitUnpinnedText { get; }
            string WarningDetailEndedText { get; }
            string WarningCurrentLevelText { get; }
        }

        public enum LagQuestState
        {
            None,
            MustProfileSelf,
            MustDelagSelf,
            MustWaitUnpinned,
            Ended, // show players that the quest is done
            Cleared, // remove the hud
        }

        public sealed class PlayerState
        {
            public LagQuestState Quest { get; set; }
            public LagWarningSource Latest { get; set; }
            public double LastWarningLagNormal { get; set; }

            public PlayerState Snapshot() => new PlayerState
            {
                Quest = Quest,
                Latest = Latest,
                LastWarningLagNormal = LastWarningLagNormal,
            };
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
                UpdateQuestLog(LagQuestState.Cleared, playerId);
            }

            _quests.Clear();
            _hudNotifications.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Remove(long playerId)
        {
            _quests.Remove(playerId);
            UpdateQuestLog(LagQuestState.Cleared, playerId);
            _hudNotifications.Remove(playerId);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Update(IEnumerable<LagWarningSource> players)
        {
            var laggyPlayers = players
                .Where(p => p.LongLagNormal >= _config.WarningLagNormal || p.IsPinned)
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
                        Quest = LagQuestState.MustProfileSelf,
                        Latest = laggyPlayer,
                        LastWarningLagNormal = laggyPlayer.LongLagNormal / _config.WarningLagNormal,
                    };

                    _quests[playerId] = newPlayerState;
                    UpdateQuestLog(newPlayerState.Quest, playerId);

                    Log.Info($"new warning issued: \"{laggyPlayer.PlayerName}\" {lag * 100:0}%");
                }
                else if (laggyPlayer.IsPinned && playerState.Quest < LagQuestState.MustWaitUnpinned)
                {
                    playerState.Quest = LagQuestState.MustWaitUnpinned;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (!(lag > _config.WarningLagNormal) && playerState.Quest <= LagQuestState.MustDelagSelf)
                {
                    playerState.Quest = LagQuestState.Ended;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (lag > _config.WarningLagNormal && playerState.Quest >= LagQuestState.Ended)
                {
                    playerState.Quest = LagQuestState.MustProfileSelf;
                    UpdateQuestLog(playerState.Quest, playerId);
                }
                else if (!laggyPlayer.IsPinned && playerState.Quest == LagQuestState.MustWaitUnpinned)
                {
                    playerState.Quest = lag > _config.WarningLagNormal ? LagQuestState.MustDelagSelf : LagQuestState.Ended;
                    UpdateQuestLog(playerState.Quest, playerId);
                }

                var playerQuestState = _quests[playerId];
                playerQuestState.Latest = laggyPlayer;
                playerQuestState.LastWarningLagNormal = laggyPlayer.LongLagNormal / _config.WarningLagNormal;

                var message = $"{_config.WarningCurrentLevelText}: {lag * 100:0}%";
                if (laggyPlayer.IsPinned)
                {
                    message += $" (punished for {laggyPlayer.Pin.TotalSeconds:0} seconds more)";
                }

                _hudNotifications.Show(playerId, message);
            }

            var latestLaggyPlayerIds = laggyPlayers.Select(s => s.PlayerId).ToSet();
            foreach (var (existingPlayerId, state) in _quests.ToArray())
            {
                // remove quests ended during the last interval
                if (state.Quest >= LagQuestState.Ended)
                {
                    _quests.Remove(existingPlayerId);
                    UpdateQuestLog(LagQuestState.Cleared, existingPlayerId);
                    continue;
                }

                // end quests of players that aren't laggy anymore
                if (!latestLaggyPlayerIds.Contains(existingPlayerId))
                {
                    state.Quest = LagQuestState.Ended;
                    UpdateQuestLog(state.Quest, existingPlayerId);
                    _hudNotifications.Remove(existingPlayerId);
                    Log.Info($"warning withdrawn: {state.Latest.PlayerName}");
                }
            }

            foreach (var (_, state) in _quests)
            {
                Log.Debug($"warning ongoing: {state.Latest} {state.Quest}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void OnSelfProfiled(long playerId)
        {
            if (_quests.TryGetValue(playerId, out var state))
            {
                var lagNormal = state.Latest.LongLagNormal;
                if (state.Quest <= LagQuestState.MustProfileSelf)
                {
                    var warningLagNormal = lagNormal / _config.WarningLagNormal;
                    state.LastWarningLagNormal = warningLagNormal;
                    state.Quest = warningLagNormal >= 1
                        ? LagQuestState.MustDelagSelf
                        : LagQuestState.MustWaitUnpinned;
                    UpdateQuestLog(state.Quest, playerId);
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyDictionary<long, PlayerState> GetInternalSnapshot()
        {
            return _quests
                .Select(q => (q.Key, q.Value.Snapshot()))
                .ToDictionary();
        }

        void UpdateQuestLog(LagQuestState quest, long playerId)
        {
            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"{playerId}");
            Log.Debug($"updating quest log: {playerName}: {quest}");

            switch (quest)
            {
                case LagQuestState.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, _config.WarningTitle, playerId);
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustProfileSelfText, true, true, playerId);
                    return;
                }
                case LagQuestState.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustDelagSelfText, true, true, playerId);
                    return;
                }
                case LagQuestState.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustWaitUnpinnedText, true, true, playerId);
                    return;
                }
                case LagQuestState.Ended:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailEndedText, true, true, playerId);
                    return;
                }
                case LagQuestState.Cleared:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(false, "", playerId);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(quest), quest, null);
            }
        }
    }
}