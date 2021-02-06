using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Sandbox.Game;

namespace AutoModerator.Quests
{
    public sealed class SelfModerationQuestCollection
    {
        enum QuestState
        {
            Invalid,
            MustProfileSelf,
            MustDelagSelf,
            MustWaitUnpinned,
            Ended,
        }

        readonly Dictionary<long, QuestState> _quests;

        public SelfModerationQuestCollection()
        {
            _quests = new Dictionary<long, QuestState>();
        }

        public void Clear()
        {
            foreach (var (playerId, _) in _quests)
            {
                MyVisualScriptLogicProvider.SetQuestlog(false, null, playerId);
            }

            _quests.Clear();
        }

        public void Update(IEnumerable<(long PlayerId, double LongLagNormal, bool Pinned)> playerStates, CancellationToken canceller)
        {
        }
    }
}