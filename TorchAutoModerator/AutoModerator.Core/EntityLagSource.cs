using Sandbox.Game.World;

namespace AutoModerator.Core
{
    public readonly struct EntityLagSource
    {
        public EntityLagSource(long entityId, string name, long ownerId, string ownerName, double lagMspf, long factionId)
        {
            EntityId = entityId;
            Name = name;
            OwnerId = ownerId;
            OwnerName = ownerName;
            LagMspf = lagMspf;
            FactionId = factionId;
        }

        public readonly long EntityId;
        public readonly string Name;
        public readonly long OwnerId;
        public readonly string OwnerName;
        public readonly double LagMspf;
        public readonly long FactionId; // for filtering

        public override string ToString()
        {
            var factionTag = MySession.Static.Factions.TryGetFactionById(FactionId)?.Tag;
            return $"[{factionTag ?? "<single>"}] \"{Name}\" {LagMspf:0.00}ms/f";
        }
    }
}