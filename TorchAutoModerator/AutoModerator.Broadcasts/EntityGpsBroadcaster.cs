using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Screens.Helpers;
using TorchEntityGpsBroadcaster.Core;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Broadcasts
{
    public sealed class EntityGpsBroadcaster
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityIdGpsCollection _gpsCollection;

        public EntityGpsBroadcaster()
        {
            _gpsCollection = new EntityIdGpsCollection("<!> ");
        }

        public IEnumerable<MyGps> GetGpss()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }

        public void ClearGpss()
        {
            _gpsCollection.SendDeleteAllGpss();
        }

        public async Task ReplaceGpss(
            IEnumerable<IEntityGpsSource> gpsSources,
            IEnumerable<long> receiverIdentityIds,
            CancellationToken canceller)
        {
            var gpss = await CreateGps(gpsSources, canceller);
            _gpsCollection.SendReplaceAllTrackedGpss(gpss, receiverIdentityIds);
        }

        async Task<IReadOnlyList<MyGps>> CreateGps(IEnumerable<IEntityGpsSource> gpsSources, CancellationToken canceller)
        {
            try
            {
                // MyGps can be created in the game loop only (idk why)
                await GameLoopObserver.MoveToGameLoop(canceller);

                var stopwatch = Stopwatch.StartNew();

                var gpss = new List<MyGps>();
                foreach (var gpsSource in gpsSources)
                {
                    if (gpsSource.TryCreateGps(out var gps))
                    {
                        gpss.Add(gps);
                    }
                }

                var timeSpent = stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();

                Log.Debug($"Creating GPSs time spent: {timeSpent:0.00}ms");

                return gpss;
            }
            finally
            {
                // make sure we're out of the main thread
                await TaskUtils.MoveToThreadPool(canceller);
            }
        }
    }
}