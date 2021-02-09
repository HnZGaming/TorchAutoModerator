using System;

namespace AutoModerator.Warnings
{
    public readonly struct LaggyPlayerSnapshot
    {
        public LaggyPlayerSnapshot(
            long playerId, string playerName,
            double longLagNormal, bool isPinned,
            double gridLongLagNormal, bool isGridPinned)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            PlayerLagNormal = longLagNormal;
            IsPlayerPinned = isPinned;
            GridLongLagNormal = gridLongLagNormal;
            IsGridPinned = isGridPinned;
        }

        public long PlayerId { get; }
        public string PlayerName { get; }

        public double PlayerLagNormal { get; }
        public bool IsPlayerPinned { get; }

        public double GridLongLagNormal { get; }
        public bool IsGridPinned { get; }

        public double LongLagNormal => Math.Max(PlayerLagNormal, GridLongLagNormal);
        public bool IsPinned => IsPlayerPinned || IsGridPinned;

        public override string ToString()
        {
            return $"\"{PlayerName}\" player: ({PlayerLagNormal * 100:0}%, {IsPlayerPinned}), grid: ({GridLongLagNormal * 100:0}%, {IsGridPinned})";
        }
    }
}