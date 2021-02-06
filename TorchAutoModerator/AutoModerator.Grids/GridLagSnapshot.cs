using AutoModerator.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;

namespace AutoModerator.Grids
{
    public sealed class GridLagSnapshot : IEntityLagSnapshot
    {
        GridLagSnapshot(long entityId, long ownerId, double lagNormal, string factionTagOrNull)
        {
            EntityId = entityId;
            OwnerId = ownerId;
            LagNormal = lagNormal;
            FactionTagOrNull = factionTagOrNull;
        }

        public long EntityId { get; }
        public long OwnerId { get; }
        public double LagNormal { get; }
        public string FactionTagOrNull { get; }

        public static GridLagSnapshot FromGrid(MyCubeGrid grid, double lagNormal)
        {
            var factionTag = (string) null;

            if (grid.BigOwners.TryGetFirst(out var playerId))
            {
                var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
                factionTag = faction?.Tag;
            }

            return new GridLagSnapshot(grid.EntityId, playerId, lagNormal, factionTag);
        }
    }
}