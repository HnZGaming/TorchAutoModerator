using System;

namespace AutoModerator.Warnings
{
    public readonly struct LagWarningSource
    {
        public LagWarningSource(
            long playerId, string playerName,
            double longLagNormal, TimeSpan pin,
            double gridLongLagNormal, TimeSpan gridPin)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            PlayerLagNormal = longLagNormal;
            PlayerPin = pin;
            GridLongLagNormal = gridLongLagNormal;
            GridPin = gridPin;
        }

        public long PlayerId { get; }
        public string PlayerName { get; }

        public double PlayerLagNormal { get; }
        public TimeSpan PlayerPin { get; }

        public double GridLongLagNormal { get; }
        public TimeSpan GridPin { get; }

        public double LongLagNormal => Math.Max(PlayerLagNormal, GridLongLagNormal);
        public TimeSpan Pin => PlayerPin > GridPin ? PlayerPin : GridPin;
        public bool IsPinned => Pin > TimeSpan.Zero;

        public override string ToString()
        {
            return $"\"{PlayerName}\" player: ({PlayerLagNormal * 100:0}%, {PlayerPin.TotalSeconds:0}secs), grid: ({GridLongLagNormal * 100:0}%, {GridPin.TotalSeconds:0}secs)";
        }
    }
}