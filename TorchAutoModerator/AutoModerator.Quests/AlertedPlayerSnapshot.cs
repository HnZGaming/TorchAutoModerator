namespace AutoModerator.Quests
{
    public readonly struct AlertedPlayerSnapshot
    {
        public AlertedPlayerSnapshot(long playerId, double lagNormal, bool isPinned)
        {
            PlayerId = playerId;
            LagNormal = lagNormal;
            IsPinned = isPinned;
        }

        public long PlayerId { get; }
        public double LagNormal { get; }
        public bool IsPinned { get; }
    }
}