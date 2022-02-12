namespace AutoModerator.Core
{
    public readonly struct EntitySource
    {
        public EntitySource(long entityId, string name, long ownerId, string ownerName, long factionId, string factionTag, double lagMspf)
        {
            EntityId = entityId;
            Name = name;
            OwnerId = ownerId;
            OwnerName = ownerName;
            FactionTag = factionTag;
            LagMspf = lagMspf;
            FactionId = factionId;
        }

        public readonly long EntityId;
        public readonly string Name;
        public readonly long OwnerId;
        public readonly string OwnerName;
        public readonly string FactionTag;
        public readonly double LagMspf;
        public readonly long FactionId; // for filtering

        public override string ToString()
        {
            return $"[{FactionTag ?? "<single>"}] \"{Name}\" {LagMspf:0.00}ms/f";
        }
    }
}