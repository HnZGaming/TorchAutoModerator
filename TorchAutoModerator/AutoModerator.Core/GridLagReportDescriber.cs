using System;

namespace AutoModerator.Core
{
    public sealed class GridLagReportDescriber
    {
        public interface IConfig
        {
            string GpsNameFormat { get; }
            string GpsDescriptionFormat { get; }
        }

        readonly IConfig _config;

        public GridLagReportDescriber(IConfig config)
        {
            _config = config;
        }

        public string MakeName(GridLagReport report, int rank)
        {
            var rankStr = RankToString(rank);
            return GridLagReportToString(report, _config.GpsNameFormat)
                .Replace("{rank}", rankStr);
        }

        public string MakeDescription(GridLagReport report, int rank)
        {
            var rankStr = RankToString(rank);
            return GridLagReportToString(report, _config.GpsDescriptionFormat)
                .Replace("{rank}", rankStr);
        }

        static string GridLagReportToString(GridLagReport report, string format)
        {
            var str = format
                .Replace("{grid}", report.GridName)
                .Replace("{player}", report.PlayerNameOrNull ?? "<none>")
                .Replace("{faction}", report.FactionTagOrNull ?? "<none>")
                .Replace("{ratio}", $"{report.ThresholdNormal * 100:0}%");

            if (report.RemainingTimeOrInfinite is TimeSpan remainingTime)
            {
                var remainingTimeStr = RemainingTimeToString(remainingTime);
                return $"{str} ({remainingTimeStr})";
            }

            return str;
        }

        static string RemainingTimeToString(TimeSpan remainingTime)
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

        static string RankToString(int rank)
        {
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