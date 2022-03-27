using System;
using NLog;

namespace AutoModerator.Punishes.Broadcasts
{
    public static class GpsUtils
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

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