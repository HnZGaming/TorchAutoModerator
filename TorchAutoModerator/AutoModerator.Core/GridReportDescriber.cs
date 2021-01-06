namespace AutoModerator.Core
{
    public sealed class GridReportDescriber
    {
        public interface IConfig
        {
            string GpsNameFormat { get; }
            string GpsDescriptionFormat { get; }
        }

        readonly IConfig _config;

        public GridReportDescriber(IConfig config)
        {
            _config = config;
        }

        public string MakeName(GridReport report, int rank)
        {
            var mspfRatio = report.ThresholdNormal * 100;
            var rankStr = RankToString(rank);

            return _config.GpsNameFormat
                .Replace("{grid}", report.GridName)
                .Replace("{player}", report.PlayerNameOrNull ?? "<none>")
                .Replace("{faction}", report.FactionTagOrNull ?? "<none>")
                .Replace("{ratio}", $"{mspfRatio:0}%")
                .Replace("{rank}", rankStr);
        }

        public string MakeDescription(GridReport report, int rank)
        {
            var mspfRatio = report.ThresholdNormal * 100;
            var rankStr = RankToString(rank);

            return _config.GpsDescriptionFormat
                .Replace("{ratio}", $"{mspfRatio:0}%")
                .Replace("{rank}", rankStr);
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