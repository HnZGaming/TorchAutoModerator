using System;
using System.Collections.Generic;

namespace Utils.TimeSerieses
{
    public interface ITimeSeries<T> : IReadOnlyList<Timestamped<T>>
    {
        TimeSpan Length { get; }
    }
}