using System;
using System.Collections.Generic;

namespace Utils.General.TimeSerieses
{
    internal sealed class TimestampComparer : Comparer<DateTime>
    {
        public override int Compare(DateTime x, DateTime y)
        {
            return x.ToBinary().CompareTo(y.ToBinary());
        }
    }
}