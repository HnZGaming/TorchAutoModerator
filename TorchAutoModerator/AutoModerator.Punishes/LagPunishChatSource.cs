namespace AutoModerator.Punishes
{
    public readonly struct LagPunishChatSource
    {
        public LagPunishChatSource(long playerId, string playerName, string factionTag, long gridId, string gridName, double longLagNormal, bool isPinned)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            FactionTag = factionTag;
            GridId = gridId;
            GridName = gridName;
            LongLagNormal = longLagNormal;
            IsPinned = isPinned;
        }

        public readonly long PlayerId;
        public readonly string PlayerName;
        public readonly string FactionTag;
        public readonly long GridId;
        public readonly string GridName;
        public readonly double LongLagNormal;
        public readonly bool IsPinned;
    }
}