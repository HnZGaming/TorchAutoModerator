using System;
using NUnit.Framework;

namespace Utils.TimeSerieses.Test
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Test1()
        {
            var timeSeries = new TimeSeries<double>();
            timeSeries.Add(DateTime.UtcNow - TimeSpan.FromSeconds(20), 0);
            timeSeries.Add(DateTime.UtcNow - TimeSpan.FromSeconds(10), 1);
            timeSeries.Add(DateTime.UtcNow, 2);

            var lastCount = timeSeries.Count;
            Assert.AreEqual(3, lastCount);

            timeSeries.RemoveOlderThan(DateTime.UtcNow - TimeSpan.FromSeconds(15));

            var nextCount = timeSeries.Count;
            Assert.AreEqual(2, nextCount);

            Assert.AreEqual(1, timeSeries.GetPointAt(0).Element);
            Assert.AreEqual(2, timeSeries.GetPointAt(1).Element);
        }
    }
}