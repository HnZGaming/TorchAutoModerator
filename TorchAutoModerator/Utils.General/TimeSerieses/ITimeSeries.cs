using System;
using System.Collections.Generic;

namespace Utils.General.TimeSerieses
{
    internal interface ITimeSeries<T>
    {
        bool IsEmpty { get; }
        IEnumerable<(DateTime Timestamp, T Element)> GetSeries();
    }
}