using System;
using NLog;
using Sandbox.Game.World;
using Torch.API.Managers;
using Utils.Torch;
using VRageMath;

namespace AutoModerator.Warnings
{
    public sealed class LagWarningChatFeed : LagWarningTracker.IQuestListener
    {
        public interface IConfig
        {
            bool EnableWarningChatFeed { get; }
            string WarningDetailMustProfileSelfText { get; }
            string WarningDetailMustDelagSelfText { get; }
            string WarningDetailMustWaitUnpinnedText { get; }
            string WarningDetailEndedText { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly IChatManagerServer _chatManager;

        public LagWarningChatFeed(IConfig config, IChatManagerServer chatManager)
        {
            _config = config;
            _chatManager = chatManager;
        }

        public void OnQuestUpdated(long playerId, LagQuest quest)
        {
            if (!_config.EnableWarningChatFeed) return;

            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"{playerId}");
            Log.Debug($"sending lag warning chat: {playerName}: {quest}");

            switch (quest)
            {
                case LagQuest.MustProfileSelf:
                {
                    SendChat(playerId, _config.WarningDetailMustProfileSelfText);
                    return;
                }
                case LagQuest.MustDelagSelf:
                {
                    SendChat(playerId, _config.WarningDetailMustDelagSelfText);
                    return;
                }
                case LagQuest.MustWaitUnpinned:
                {
                    SendChat(playerId, _config.WarningDetailMustWaitUnpinnedText);
                    return;
                }
                case LagQuest.Ended:
                {
                    SendChat(playerId, _config.WarningDetailEndedText);
                    return;
                }
                case LagQuest.Cleared:
                {
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(quest), quest, null);
            }
        }

        void SendChat(long playerId, string message)
        {
            var steamId = MySession.Static.Players.TryGetSteamId(playerId);
            if (steamId == 0) return;

            _chatManager.SendMessageAsOther("AutoModerator", message, Color.Red, steamId);
        }
    }
}