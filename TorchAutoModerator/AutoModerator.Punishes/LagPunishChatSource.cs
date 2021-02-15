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

        public readonly long PlayerId;
        public readonly long LaggiestGridId;
        public readonly double LongLagNormal;
        public readonly bool IsPinned;
    }
}