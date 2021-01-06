using System.Linq;

namespace AutoModerator.Core
{
    public sealed class LaggyGridGpsDescriptionMaker
    {
        public interface IConfig
        {
            string GpsDescriptionFormat { get; }
        }

        readonly IConfig _config;

        public LaggyGridGpsDescriptionMaker(IConfig config)
        {
            _config = config;
        }

        public string Make(LaggyGridReport report, int rank)
        {
            var mspfRatio = report.MspfRatio * 100;
            var rankStr = RankToString(rank);

            return _config.GpsDescriptionFormat
                .Replace("{ratio}", $"{mspfRatio:0}%")
                .Replace("{rank}", rankStr);
        }

        static string RankToString(int rank)
        {
            switch ($"{rank}".Last())
            {
                case '1': return $"{rank}st";
                case '2': return $"{rank}nd";
                case '3': return $"{rank}rd";
                default: return $"{rank}th";
            }
        }
    }
}