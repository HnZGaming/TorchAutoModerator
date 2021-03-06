using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Utils.TimeSerieses
{
    public sealed class ReadOnlyTimeSeries<T> : ITimeSeries<T>
    {
        readonly List<Timestamped<T>> _source;
        readonly List<T> _elements;

        public ReadOnlyTimeSeries(IEnumerable<Timestamped<T>> source)
        {
            _source = source.ToList();
            _elements = source.Select(e => e.Element).ToList();
        }

        public IEnumerator<Timestamped<T>> GetEnumerator() => _source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => _source.Count;
        public Timestamped<T> this[int index] => _source[index];
        public IReadOnlyList<T> Elements => _elements;
    }
}