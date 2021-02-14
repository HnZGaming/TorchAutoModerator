using System;
using System.Collections.Generic;
using MathNet.Numerics.Statistics;

namespace Utils.TimeSerieses
{
    public static class TimeSeriesUtils
    {
        public static IReadOnlyList<double> TestOutlier(this ITimeSeries<double> self, bool abs = true)
        {
            var tests = new List<double>();
            var mean = self.Elements.Mean();
            var stdev = self.Elements.StandardDeviation();
            foreach (var src in self.Elements)
            {
                var test = (src - mean) / stdev;
                test = abs ? Math.Abs(test) : test;
                tests.Add(test);
            }

            return tests;
        }

        public static bool IsAllYoungerThan<T>(this ITimeSeries<T> self, DateTime timestamp)
        {
            var oldestTimestamp = self[0].Timestamp;
            return oldestTimestamp > timestamp;
        }
    }
}