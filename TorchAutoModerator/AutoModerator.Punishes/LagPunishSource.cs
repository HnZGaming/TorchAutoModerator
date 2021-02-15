namespace AutoModerator.Punishes
{
    public readonly struct LagPunishSource
    {
        public LagPunishSource(long gridId, bool isPinned)
        {
            GridId = gridId;
            IsPinned = isPinned;
        }

        public readonly long GridId;
        public readonly bool IsPinned;
    }
}