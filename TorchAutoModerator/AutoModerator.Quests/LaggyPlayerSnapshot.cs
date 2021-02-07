namespace AutoModerator.Quests
{
    public readonly struct LaggyPlayerSnapshot
    {
        public LaggyPlayerSnapshot(long playerId, double longLagNormal, bool isPinned)
        {
            PlayerId = playerId;
            LongLagNormal = longLagNormal;
            IsPinned = isPinned;
        }

        public long PlayerId { get; }
        public double LongLagNormal { get; }
        public bool IsPinned { get; }
    }
}