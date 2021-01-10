using System;
using System.Text;
using Utils.General.TimeSerieses;

namespace AutoModerator.Core
{
    internal static class Utils
    {
        public static string ToTimelineString(this ITimeSeries<GridLagProfileResult> self, string timestampFormat, int width)
        {
            var sb = new StringBuilder();
            foreach (var (timestamp, element) in self.GetSeries())
            {
                sb.Append(timestamp.ToString(timestampFormat));
                sb.Append(' ');

                var normal = Math.Min(1, element.ThresholdNormal);
                var starIndex = (int) (width * normal);
                for (var i = 0; i < width; i++)
                {
                    var c = i == starIndex ? '+' : '-';
                    sb.Append(c);
                }

                sb.Append(' ');
                sb.Append($"{element.ThresholdNormal * 100:0}%");
            }

            return sb.ToString();
        }
    }
}