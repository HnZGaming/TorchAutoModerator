using System;

namespace AutoModerator.Quests
{
    public readonly struct QuestSource
    {
        public QuestSource(
            long playerId, string playerName, double playerLagNormal, TimeSpan playerPin,
            long gridId, double gridLagNormal, TimeSpan gridPin)
        {
            PlayerId = playerId;
            PlayerName = playerName;

            if (playerPin > gridPin || playerLagNormal > gridLagNormal)
            {
                EntityId = playerId;
                LagNormal = playerLagNormal;
                Pin = playerPin;
            }
            else
            {
                EntityId = gridId;
                LagNormal = gridLagNormal;
                Pin = gridPin;
            }
        }

        public readonly long PlayerId;
        public readonly string PlayerName;
        public readonly double LagNormal;
        public readonly TimeSpan Pin;
        public readonly long EntityId;

        public override string ToString()
        {
            return $"\"{PlayerName}\" ({PlayerId}, {EntityId}) {LagNormal * 100:0}%, pin({Pin.TotalSeconds:0}secs)";
        }
    }
}