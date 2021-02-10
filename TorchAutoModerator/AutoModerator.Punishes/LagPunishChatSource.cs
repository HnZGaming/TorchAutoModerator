namespace AutoModerator.Punishes
{
    public readonly struct LagPunishChatSource
    {
        public LagPunishChatSource(long playerId, long laggiestGridId, double longLagNormal)
        {
            PlayerId = playerId;
            LaggiestGridId = laggiestGridId;
            LongLagNormal = longLagNormal;
        }

        public long PlayerId { get; }
        public long LaggiestGridId { get; }
        public double LongLagNormal { get; }
    }
}