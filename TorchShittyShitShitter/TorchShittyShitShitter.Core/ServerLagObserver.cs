using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game.Multiplayer;
using Utils.General;

namespace TorchShittyShitShitter.Core
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
        readonly int _bufferSeconds;
        readonly Queue<double> _timeline;

        public ServerLagObserver(IConfig config, int bufferSeconds)
        {
            _config = config;
            _bufferSeconds = bufferSeconds;
            _timeline = new Queue<double>();
        }

        public bool IsLaggy { get; private set; }

        public async Task LoopObserving(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var ss = Sync.ServerSimulationRatio;
                _timeline.Enqueue(ss);

                while (_timeline.Count > _bufferSeconds)
                {
                    _timeline.TryDequeue(out _);
                }

                var referenceSimSpeed = _timeline.Max();
                IsLaggy = referenceSimSpeed < _config.SimSpeedThreshold;

                await canceller.Delay(1.Seconds());
            }
        }
    }
}