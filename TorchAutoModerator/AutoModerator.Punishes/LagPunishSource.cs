namespace AutoModerator.Punishes
{
    public readonly struct LagPunishSource
    {
        public LagPunishSource(long gridId, bool isPinned)
        {
            GridId = gridId;
            IsPinned = isPinned;
        }

        public long GridId { get; }
        public bool IsPinned { get; }
    }
}