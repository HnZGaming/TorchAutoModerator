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

        public static TimeSpan GetTimeLength<T>(this ITimeSeries<T> self)
        {
            if (self.Count < 2) return TimeSpan.Zero;
            var firstTimestamp = self[0].Timestamp;
            var lastTimestamp = self[self.Count - 1].Timestamp;

            return lastTimestamp - firstTimestamp;
        }

        public static ITimeSeries<T> GetScoped<T>(this ITimeSeries<T> self, DateTime from)
        {
            if (self[0].Timestamp > from) return self; // within the scope
            if (self[self.Count - 1].Timestamp < from) return new TimeSeries<T>(); // empty

            var copy = new List<Timestamped<T>>();
            foreach (var t in self)
            {
                if (t.Timestamp > from)
                {
                    copy.Add(t);
                }
            }

            return new ReadOnlyTimeSeries<T>(copy);
        }
    }
}