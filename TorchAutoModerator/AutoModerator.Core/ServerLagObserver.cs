using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game.Multiplayer;
using Utils.General;

namespace AutoModerator.Core
{
    /// <summary>
    /// Observe when the server is laggy.
    /// </summary>
    public sealed class ServerLagObserver
    {
        readonly TimeSpan _buffer;
        readonly Queue<double> _timeline;

        public ServerLagObserver(TimeSpan buffer)
        {
            _buffer = buffer;
            _timeline = new Queue<double>();
        }

        public double SimSpeed { get; private set; }

        public async Task Observe(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var ss = Sync.ServerSimulationRatio;
                _timeline.Enqueue(ss);

                while (_timeline.Count > _buffer.TotalSeconds)
                {
                    _timeline.TryDequeue(out _);
                }

                SimSpeed = _timeline.Average();

                await Task.Delay(1.Seconds(), canceller);
            }
        }
    }
}