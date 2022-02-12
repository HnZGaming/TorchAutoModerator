namespace AutoModerator.Punishes
{
    public readonly struct PunishSource
    {
        public PunishSource(long playerId, string playerName, string factionTag, long gridId, string gridName, double lagNormal, bool isPinned)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            FactionTag = factionTag;
            GridId = gridId;
            GridName = gridName;
            LagNormal = lagNormal;
            IsPinned = isPinned;
        }

        public readonly long PlayerId;
        public readonly string PlayerName;
        public readonly string FactionTag;
        public readonly long GridId;
        public readonly string GridName;
        public readonly double LagNormal;
        public readonly bool IsPinned;

        public override string ToString()
        {
            return $"player: [{FactionTag}] \"{PlayerName}\" <{PlayerId}>, grid: \"{GridName}\" <{GridId}>, {LagNormal*100:0.0}%, pinned: {IsPinned}";
        }
    }
}