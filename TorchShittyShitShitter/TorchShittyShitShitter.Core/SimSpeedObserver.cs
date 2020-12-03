using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sandbox.Game.Multiplayer;
using Utils.General;

namespace TorchShittyShitShitter.Core
{
    public sealed class SimSpeedObserver
    {
        public interface IConfig
        {
            double ThresholdSimSpeed { get; }
        }

        readonly IConfig _config;
        readonly int _bufferSeconds;
        readonly Queue<double> _timeline;

        public SimSpeedObserver(IConfig config, int bufferSeconds)
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
                IsLaggy = referenceSimSpeed < _config.ThresholdSimSpeed;

                await canceller.Delay(1.Seconds());
            }
        }
    }
}