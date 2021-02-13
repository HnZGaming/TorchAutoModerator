namespace AutoModerator.Core
{
    public readonly struct EntityLagSource
    {
        public EntityLagSource(long entityId, string name, double lagMspf, string factionTag)
        {
            EntityId = entityId;
            Name = name;
            LagMspf = lagMspf;
            FactionTag = factionTag;
        }

        public long EntityId { get; }
        public string Name { get; }
        public double LagMspf { get; }
        public string FactionTag { get; } // for exempt filtering
    }
}