using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Core
{
    // this class shouldn't hold onto any game entities so it won't mess with the game's GC
    public sealed class GridLagProfileResult
    {
        GridLagProfileResult(long gridId,
            double thresholdNormal,
            string gridName,
            string factionTag = null,
            string playerName = null)
        {
            GridId = gridId;
            ThresholdNormal = thresholdNormal;
            GridName = gridName;
            FactionTagOrNull = factionTag;
            PlayerNameOrNull = playerName;
        }

        public long GridId { get; }
        public double ThresholdNormal { get; }
        public string GridName { get; }
        public string FactionTagOrNull { get; }
        public string PlayerNameOrNull { get; }

        public override string ToString()
        {
            return $"\"{GridName}\" {ThresholdNormal * 100f:0.00}% [{FactionTagOrNull}] {PlayerNameOrNull}";
        }

        public static GridLagProfileResult FromGrid(IMyCubeGrid grid, double thresholdNormal)
        {
            var playerName = (string) null;

            if (grid.BigOwners.TryGetFirst(out var playerId) &&
                MySession.Static.Players.TryGetPlayerById(playerId, out var player))
            {
                playerName = player.DisplayName;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var factionTag = faction?.Tag;

            return new GridLagProfileResult(grid.EntityId, thresholdNormal, grid.DisplayName, factionTag, playerName);
        }
    }
}