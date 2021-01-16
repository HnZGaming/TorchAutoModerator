namespace Utils.TimeSerieses
{
    public interface ITimeSeries<T>
    {
        int Count { get; }
        Timestamped<T> GetPointAt(int index);
    }
}