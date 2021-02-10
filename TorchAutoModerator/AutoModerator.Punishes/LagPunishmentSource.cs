namespace AutoModerator.Punishes
{
    public readonly struct LagPunishmentSource
    {
        public LagPunishmentSource(long gridId, bool isPinned)
        {
            GridId = gridId;
            IsPinned = isPinned;
        }

        public long GridId { get; }
        public bool IsPinned { get; }
    }
}