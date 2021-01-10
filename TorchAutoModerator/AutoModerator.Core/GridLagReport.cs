using System;

namespace AutoModerator.Core
{
    public sealed class GridLagReport
    {
        public GridLagReport(GridLagProfileResult profileResult, TimeSpan? remainingTimeOrInfinite)
        {
            GridId = profileResult.GridId;
            ThresholdNormal = profileResult.ThresholdNormal;
            GridName = profileResult.GridName;
            FactionTagOrNull = profileResult.FactionTagOrNull;
            PlayerNameOrNull = profileResult.PlayerNameOrNull;
            RemainingTimeOrInfinite = remainingTimeOrInfinite;
        }

        public long GridId { get; }
        public double ThresholdNormal { get; }
        public string GridName { get; }
        public string FactionTagOrNull { get; }
        public string PlayerNameOrNull { get; }
        public TimeSpan? RemainingTimeOrInfinite { get; }

        public override string ToString()
        {
            var normal = $"{ThresholdNormal * 100f:0.00}%";
            var remainingTime = $"{RemainingTimeOrInfinite?.TotalMinutes ?? double.PositiveInfinity:0.0}m";
            var factionTag = FactionTagOrNull ?? "<single>";
            var playerName = PlayerNameOrNull ?? "<none>";
            return $"\"{GridName}\" ({GridId}) {normal} for {remainingTime} [{factionTag}] {playerName}";
        }
    }
}