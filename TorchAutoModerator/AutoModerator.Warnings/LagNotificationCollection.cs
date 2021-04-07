using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;

namespace AutoModerator.Warnings
{
    public sealed class LagNotificationCollection : LagWarningTracker.ILagListener
    {
        public interface IConfig
        {
            bool EnableLagWarningNotification { get; }
            string WarningCurrentLevelText { get; }
        }

        readonly IConfig _config;
        readonly ConcurrentDictionary<long, int> _notificationIds;

        public LagNotificationCollection(IConfig config)
        {
            _config = config;
            _notificationIds = new ConcurrentDictionary<long, int>();
        }

        public void OnLagCleared(long playerId)
        {
            Remove(playerId);
        }

        public void OnLagUpdated(LagWarningSource player)
        {
            var playerId = player.PlayerId;
            var lag = player.LongLagNormal;

            Remove(playerId);

            if (!_config.EnableLagWarningNotification) return;

            var message = $"{_config.WarningCurrentLevelText}: {lag * 100:0}%";
            if (player.IsPinned)
            {
                message += $" (punished for {player.Pin.TotalSeconds:0} seconds more)";
            }

            var id = MyVisualScriptLogicProvider.AddNotification(message, "Red", playerId);
            _notificationIds[playerId] = id;
        }

        void Remove(long playerId)
        {
            if (_notificationIds.TryGetValue(playerId, out var id))
            {
                MyVisualScriptLogicProvider.RemoveNotification(id);
                _notificationIds.Remove(playerId);
            }
        }
    }
}