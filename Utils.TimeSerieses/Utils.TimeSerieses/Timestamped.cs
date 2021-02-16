using System;

namespace Utils.TimeSerieses
{
    public readonly struct Timestamped<T>
    {
        public readonly DateTime Timestamp;
        public readonly T Element;

        public Timestamped(DateTime timestamp, T element)
        {
            Timestamp = timestamp;
            Element = element;
        }

        public void Deconstruct(out DateTime timestamp, out T element)
        {
            timestamp = Timestamp;
            element = Element;
        }
    }
}