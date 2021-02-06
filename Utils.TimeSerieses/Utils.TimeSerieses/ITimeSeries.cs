using System;

namespace Utils.TimeSerieses
{
    public interface ITimeSeries<T>
    {
        int Count { get; }
        TimeSpan Length { get; }
        Timestamped<T> GetPointAt(int index);
    }
}