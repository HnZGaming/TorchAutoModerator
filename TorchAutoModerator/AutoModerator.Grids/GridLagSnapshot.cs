using AutoModerator.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Grids
{
    // this class shouldn't hold onto any game entities so it won't mess with the game's GC
    public sealed class GridLagSnapshot : IEntityLagSnapshot
    {
        GridLagSnapshot(long entityId,
            double lagNormal,
            string gridName,
            string factionTag = null,
            string playerName = null)
        {
            EntityId = entityId;
            LagNormal = lagNormal;
            GridName = gridName;
            FactionTagOrNull = factionTag;
            PlayerNameOrNull = playerName;
        }

        public long EntityId { get; }
        public double LagNormal { get; }
        public string GridName { get; }
        public string FactionTagOrNull { get; }
        public string PlayerNameOrNull { get; }

        public override string ToString()
        {
            return $"\"{GridName}\" {LagNormal * 100f:0.00}% [{FactionTagOrNull}] {PlayerNameOrNull}";
        }

        public static GridLagSnapshot FromGrid(MyCubeGrid grid, double lagNormal)
        {
            var playerName = (string) null;

            if (grid.BigOwners.TryGetFirst(out var playerId) &&
                MySession.Static.Players.TryGetPlayerById(playerId, out var player))
            {
                playerName = player.DisplayName;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var factionTag = faction?.Tag;

            return new GridLagSnapshot(grid.EntityId, lagNormal, grid.DisplayName, factionTag, playerName);
        }
    }
}