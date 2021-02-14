using Sandbox.Game.World;

namespace AutoModerator.Core
{
    public readonly struct EntityLagSource
    {
        public EntityLagSource(long entityId, string name, double lagMspf, long factionId)
        {
            EntityId = entityId;
            Name = name;
            LagMspf = lagMspf;
            FactionId = factionId;
        }

        public long EntityId { get; }
        public string Name { get; }
        public double LagMspf { get; }
        public long FactionId { get; } // for filtering

        public override string ToString()
        {
            var factionTag = MySession.Static.Factions.TryGetFactionById(FactionId)?.Tag;
            return $"\"{Name}\" [{factionTag ?? "<single>"}] {LagMspf:0.00}ms/f";
        }
    }
}