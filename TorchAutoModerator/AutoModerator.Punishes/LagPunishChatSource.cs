namespace AutoModerator.Punishes
{
    public readonly struct LagPunishChatSource
    {
        public LagPunishChatSource(long playerId, long laggiestGridId, double longLagNormal, bool isPinned)
        {
            PlayerId = playerId;
            LaggiestGridId = laggiestGridId;
            LongLagNormal = longLagNormal;
            IsPinned = isPinned;
        }

        public long PlayerId { get; }
        public long LaggiestGridId { get; }
        public double LongLagNormal { get; }
        public bool IsPinned { get; }
    }
}