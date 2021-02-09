using System;

namespace AutoModerator.Quests
{
    public readonly struct LaggyPlayerSnapshot
    {
        public LaggyPlayerSnapshot(
            long playerId, double longLagNormal, bool isPinned,
            double gridLongLagNormal, bool isGridPinned)
        {
            PlayerId = playerId;
            PlayerLagNormal = longLagNormal;
            IsPlayerPinned = isPinned;
            GridLongLagNormal = gridLongLagNormal;
            IsGridPinned = isGridPinned;
        }

        public long PlayerId { get; }

        public double PlayerLagNormal { get; }
        public bool IsPlayerPinned { get; }

        public double GridLongLagNormal { get; }
        public bool IsGridPinned { get; }

        public double LongLagNormal => Math.Max(PlayerLagNormal, GridLongLagNormal);
        public bool IsPinned => IsPlayerPinned || IsGridPinned;
    }
}