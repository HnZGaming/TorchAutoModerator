using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Core
{
    /// <summary>
    /// Carry around a profiled grid's metadata.
    /// </summary>
    public class GridReport
    {
        public GridReport(long gridId,
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

        public GridReport WithThresholdNormal(double thresholdNormal)
        {
            return new GridReport(GridId, thresholdNormal, GridName, FactionTagOrNull, PlayerNameOrNull);
        }

        public override string ToString()
        {
            var name = FactionTagOrNull ?? PlayerNameOrNull ?? GridName;
            return $"\"{name}\" (\"{GridName}\"), {ThresholdNormal * 100f:0.00}%";
        }

        public static GridReport FromGrid(IMyCubeGrid grid, double thresholdNormal)
        {
            var playerName = (string) null;

            if (grid.BigOwners.TryGetFirst(out var playerId) &&
                MySession.Static.Players.TryGetPlayerById(playerId, out var player))
            {
                playerName = player.DisplayName;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var factionTag = faction?.Tag;

            return new GridReport(grid.EntityId, thresholdNormal, grid.DisplayName, factionTag, playerName);
        }
    }
}