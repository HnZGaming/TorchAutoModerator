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
        public interface IConfig
        {
            double SimSpeedThreshold { get; }
        }

        readonly IConfig _config;
        readonly TimeSpan _buffer;
        readonly Queue<double> _timeline;

        public ServerLagObserver(IConfig config, TimeSpan buffer)
        {
            _config = config;
            _buffer = buffer;
            _timeline = new Queue<double>();
        }

        public bool IsLaggy { get; private set; }

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

                var referenceSimSpeed = _timeline.Max(); // best sim speed
                IsLaggy = referenceSimSpeed < _config.SimSpeedThreshold;

                await Task.Delay(1.Seconds(), canceller);
            }
        }
    }
}