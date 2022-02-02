using System;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.API.Managers;
using Utils.Torch;
using VRageMath;

namespace AutoModerator.Quests
{
    public sealed class QuestEntity
    {
        public interface IConfig
        {
            double QuestLagNormal { get; }
            bool EnableNotification { get; }
            string NotificationCurrentText { get; }
            bool EnableQuest { get; }
            string QuestTitle { get; }
            string QuestDetailMustProfileSelfText { get; }
            string QuestDetailMustDelagSelfText { get; }
            string QuestDetailMustWaitUnpinnedText { get; }
            string QuestDetailEndedText { get; }
            bool EnableQuestChatFeed { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly IConfig _config;
        readonly IChatManagerServer _chatManager;
        int _notificationId;
        int _commandNotificationId;
        bool _selfProfiled;
        bool _endCalled;

        public QuestEntity(long playerId, IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            PlayerId = playerId;
            _chatManager = chatManager;
        }

        public long PlayerId { get; }
        public string PlayerName { get; private set; }
        public long EntityId { get; private set; }
        public double LagNormal { get; private set; }
        public double QuestLagNormal { get; private set; }
        TimeSpan Pin { get; set; }
        public Quest Quest { get; private set; }

        public void Update(QuestSource source)
        {
            if (source.PlayerId != PlayerId)
            {
                throw new InvalidOperationException("player id mismatch");
            }

            if (source.LagNormal < _config.QuestLagNormal && source.Pin == TimeSpan.Zero)
            {
                throw new InvalidOperationException("shouldn't be here");
            }
            
            Log.Trace($"QuestEntity.Update({source})");

            EntityId = source.EntityId;
            PlayerName = source.PlayerName;
            LagNormal = source.LagNormal;
            QuestLagNormal = source.LagNormal / _config.QuestLagNormal;
            Pin = source.Pin;
            UpdateQuest();
        }

        public void OnSelfProfiled()
        {
            _selfProfiled = true;
            UpdateQuest();
        }

        public void End()
        {
            _endCalled = true;
            UpdateQuest();
        }

        void UpdateQuest()
        {
            var lastQuest = Quest;
            if (lastQuest == Quest.Ended)
            {
                // pass
            }
            else if (_endCalled)
            {
                Quest = Quest.Ended;
            }
            else if (Pin > TimeSpan.Zero)
            {
                Quest = Quest.MustWaitUnpinned;
            }
            else if (_selfProfiled)
            {
                Quest = Quest.MustDelagSelf;
            }
            else
            {
                Quest = Quest.MustProfileSelf;
            }

            if (Quest != lastQuest)
            {
                UpdateQuestLog();
                SendQuestChat();
            }

            UpdateNotification();
            UpdateCommandNotification();
        }

        public void Clear()
        {
            MyVisualScriptLogicProvider.RemoveNotification(_notificationId);
            MyVisualScriptLogicProvider.RemoveNotification(_commandNotificationId);
            MyVisualScriptLogicProvider.SetQuestlog(false, "", PlayerId);
        }

        public static void Clear(long playerId)
        {
            MyVisualScriptLogicProvider.SetQuestlog(false, "", playerId);
        }

        void UpdateNotification()
        {
            MyVisualScriptLogicProvider.RemoveNotification(_notificationId);

            if (!_config.EnableNotification) return;

            var message = $"{_config.NotificationCurrentText}: {LagNormal * 100:0}%";
            if (Pin > TimeSpan.Zero)
            {
                message += $" (punishment left: {Pin.TotalSeconds:0} seconds or longer)";
            }

            _notificationId = MyVisualScriptLogicProvider.AddNotification(message, "Red", PlayerId);
        }

        void UpdateCommandNotification()
        {
            MyVisualScriptLogicProvider.RemoveNotification(_commandNotificationId);

            if (Quest >= Quest.Ended) return;

            if (_selfProfiled)
            {
                const string Msg = "Type in chat: !lag inspect";
                _commandNotificationId = MyVisualScriptLogicProvider.AddNotification(Msg, "Green", PlayerId);
            }
            else
            {
                const string Msg = "Type in chat: !lag profile";
                _commandNotificationId = MyVisualScriptLogicProvider.AddNotification(Msg, "Green", PlayerId);
            }
        }

        void UpdateQuestLog()
        {
            if (!_config.EnableQuest) return;

            var playerName = MySession.Static.Players.GetPlayerNameOrElse(PlayerId, $"{PlayerId}");
            Log.Debug($"updating quest log: {playerName}: {Quest}");

            switch (Quest)
            {
                case Quest.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, _config.QuestTitle, PlayerId);
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(PlayerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.QuestDetailMustProfileSelfText, true, true, PlayerId);
                    return;
                }
                case Quest.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(PlayerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.QuestDetailMustDelagSelfText, true, true, PlayerId);
                    return;
                }
                case Quest.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(PlayerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.QuestDetailMustWaitUnpinnedText, true, true, PlayerId);
                    return;
                }
                case Quest.Ended:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(PlayerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.QuestDetailEndedText, true, true, PlayerId);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(Quest), Quest, null);
            }
        }

        void SendQuestChat()
        {
            if (!_config.EnableQuestChatFeed) return;

            var playerName = MySession.Static.Players.GetPlayerNameOrElse(PlayerId, $"{PlayerId}");
            Log.Debug($"sending quest chat: {playerName}: {Quest}");

            switch (Quest)
            {
                case Quest.MustProfileSelf:
                {
                    SendChat(_config.QuestDetailMustProfileSelfText);
                    return;
                }
                case Quest.MustDelagSelf:
                {
                    SendChat(_config.QuestDetailMustDelagSelfText);
                    return;
                }
                case Quest.MustWaitUnpinned:
                {
                    SendChat(_config.QuestDetailMustWaitUnpinnedText);
                    return;
                }
                case Quest.Ended:
                {
                    SendChat(_config.QuestDetailEndedText);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(Quest), Quest, null);
            }
        }

        void SendChat(string message)
        {
            var steamId = MySession.Static.Players.TryGetSteamId(PlayerId);
            if (steamId == 0) return;

            _chatManager.SendMessageAsOther("AutoModerator", message, Color.Red, steamId);
        }

        public override string ToString()
        {
            return $"{{\"{PlayerName}\" {Quest} {LagNormal * 100:0}% ({QuestLagNormal * 100:0}%) pin({Pin.TotalSeconds:0}secs)}}";
        }
    }
}