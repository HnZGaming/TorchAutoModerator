using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game;

namespace AutoModerator.Warnings
{
    public sealed class HudNotificationCollection
    {
        readonly ConcurrentDictionary<long, int> _notificationIds;

        public HudNotificationCollection()
        {
            _notificationIds = new ConcurrentDictionary<long, int>();
        }

        public void Show(long playerId, string message)
        {
            Remove(playerId);

            var id = MyVisualScriptLogicProvider.AddNotification(message, "Red", playerId);
            _notificationIds[playerId] = id;
        }

        public void Remove(long playerId)
        {
            if (_notificationIds.TryGetValue(playerId, out var id))
            {
                MyVisualScriptLogicProvider.RemoveNotification(id);
                _notificationIds.Remove(playerId);
            }
        }

        public void Clear()
        {
            foreach (var id in _notificationIds.Values)
            {
                MyVisualScriptLogicProvider.RemoveNotification(id);
            }
        }
    }
}