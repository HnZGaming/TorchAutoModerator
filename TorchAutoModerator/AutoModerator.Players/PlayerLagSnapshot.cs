using AutoModerator.Core;
using Sandbox.Game.World;

namespace AutoModerator.Players
{
    public sealed class PlayerLagSnapshot : IEntityLagSnapshot
    {
        public PlayerLagSnapshot(long entityId, double lagNormal, string playerName, string factionTagOrNull, long signatureGridId)
        {
            EntityId = entityId;
            PlayerName = playerName;
            FactionTagOrNull = factionTagOrNull;
            SignatureGridId = signatureGridId;
            LagNormal = lagNormal;
        }

        public long EntityId { get; }
        public double LagNormal { get; }
        public string PlayerName { get; }
        public string FactionTagOrNull { get; }
        public long SignatureGridId { get; }

        public override string ToString()
        {
            return $"\"{PlayerName}\" {LagNormal * 100f:0.00}% [{FactionTagOrNull}] {SignatureGridId}";
        }

        public static PlayerLagSnapshot FromPlayer(MyIdentity player, double lagNormal, long signatureGridId)
        {
            var faction = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
            return new PlayerLagSnapshot(player.IdentityId, lagNormal, player.DisplayName, faction?.Tag, signatureGridId);
        }
    }
}