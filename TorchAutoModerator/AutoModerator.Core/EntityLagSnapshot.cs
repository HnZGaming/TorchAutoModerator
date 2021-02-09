namespace AutoModerator.Core
{
    public readonly struct EntityLagSnapshot
    {
        public EntityLagSnapshot(long entityId, double lag, string factionTag)
        {
            EntityId = entityId;
            Lag = lag;
            FactionTag = factionTag;
        }

        public long EntityId { get; }
        public double Lag { get; }
        public string FactionTag { get; } // for exempt filtering
    }
}