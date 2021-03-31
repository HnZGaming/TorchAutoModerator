using System;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using Utils.Torch;

namespace AutoModerator.Warnings
{
    public sealed class LagQuestlogCollection : LagWarningTracker.IQuestListener
    {
        public interface IConfig
        {
            bool EnableWarningQuestlog { get; }
            string WarningTitle { get; }
            string WarningDetailMustProfileSelfText { get; }
            string WarningDetailMustDelagSelfText { get; }
            string WarningDetailMustWaitUnpinnedText { get; }
            string WarningDetailEndedText { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;

        public LagQuestlogCollection(IConfig config)
        {
            _config = config;
        }

        public void OnQuestUpdated(long playerId, LagQuest quest)
        {
            if (!_config.EnableWarningQuestlog) return;

            var playerName = MySession.Static.Players.GetPlayerNameOrElse(playerId, $"{playerId}");
            Log.Debug($"updating quest log: {playerName}: {quest}");

            switch (quest)
            {
                case LagQuest.MustProfileSelf:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(true, _config.WarningTitle, playerId);
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustProfileSelfText, true, true, playerId);
                    return;
                }
                case LagQuest.MustDelagSelf:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustDelagSelfText, true, true, playerId);
                    return;
                }
                case LagQuest.MustWaitUnpinned:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailMustWaitUnpinnedText, true, true, playerId);
                    return;
                }
                case LagQuest.Ended:
                {
                    MyVisualScriptLogicProvider.RemoveQuestlogDetails(playerId);
                    MyVisualScriptLogicProvider.AddQuestlogDetail(_config.WarningDetailEndedText, true, true, playerId);
                    return;
                }
                case LagQuest.Cleared:
                {
                    MyVisualScriptLogicProvider.SetQuestlog(false, "", playerId);
                    return;
                }
                default: throw new ArgumentOutOfRangeException(nameof(quest), quest, null);
            }
        }
    }
}