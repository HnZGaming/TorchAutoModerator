using AutoModerator.Core;
using Sandbox.Game.World;

namespace AutoModerator.Players
{
    public sealed class PlayerLagSnapshot : IEntityLagSnapshot
    {
        PlayerLagSnapshot(long entityId, double lagNormal, string factionTagOrNull)
        {
            EntityId = entityId;
            LagNormal = lagNormal;
            FactionTagOrNull = factionTagOrNull;
        }

        public long EntityId { get; }
        public double LagNormal { get; }
        public string FactionTagOrNull { get; }

        public static PlayerLagSnapshot FromPlayer(MyIdentity player, double lagNormal)
        {
            var faction = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
            return new PlayerLagSnapshot(player.IdentityId, lagNormal, faction?.Tag);
        }
    }
}