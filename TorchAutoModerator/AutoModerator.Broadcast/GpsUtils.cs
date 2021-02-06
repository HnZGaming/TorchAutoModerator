using System;
using System.Threading;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Utils.Torch;
using VRage;
using VRage.Game;
using VRageMath;

namespace AutoModerator.Broadcast
{
    public static class GpsUtils
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public static bool TryGetGridById(long gridId, out MyCubeGrid grid)
        {
            if (!MyEntityIdentifier.TryGetEntity(gridId, out var entity, true))
            {
                Log.Warn($"Grid not found by EntityId: {gridId}");
                grid = null;
                return false;
            }

            if (entity.Closed)
            {
                Log.Warn($"Grid found but closed: {gridId}");
                grid = null;
                return false;
            }

            if (!(entity is MyCubeGrid g))
            {
                throw new Exception($"Not a grid: {gridId} -> {entity.GetType()}");
            }

            grid = g;
            return true;
        }

        public static MyGps CreateGridGps(MyCubeGrid grid, string name, string description, Color color)
        {
            if (!Thread.CurrentThread.IsSessionThread())
            {
                throw new Exception("Can be called in the game loop only");
            }
            
            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = name,
                DisplayName = name,
                coords = grid.PositionComp.GetPosition(),
                showOnHud = true,
                color = color,
                description = description,
            });

            gps.SetEntity(grid);
            gps.UpdateHash();

            return gps;
        }

        public static string RemainingTimeToString(TimeSpan remainingTime)
        {
            if (remainingTime.TotalHours >= 1)
            {
                return $"{remainingTime.TotalHours:0} hours";
            }

            if (remainingTime.TotalMinutes >= 1)
            {
                return $"{remainingTime.TotalMinutes:0} minutes";
            }

            return $"{remainingTime.TotalSeconds:0} seconds";
        }

        public static string RankToString(int rank)
        {
            rank += 1;
            switch (rank % 10)
            {
                case 1: return $"{rank}st";
                case 2: return $"{rank}nd";
                case 3: return $"{rank}rd";
                default: return $"{rank}th";
            }
        }
    }
}