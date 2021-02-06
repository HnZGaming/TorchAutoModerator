using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Broadcast
{
    public sealed class EntityGpsCreator
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        public async Task<IReadOnlyList<MyGps>> Create(IEnumerable<IEntityGpsSource> sources, CancellationToken canceller)
        {
            try
            {
                // MyGps can be created in the game loop only (idk why)
                await GameLoopObserver.MoveToGameLoop(canceller);

                var gpss = new List<MyGps>();
                foreach (var gpsSource in sources)
                {
                    if (gpsSource.TryCreateGps(out var gps))
                    {
                        gpss.Add(gps);
                        Log.Trace($"broadcasting: {gpsSource}");
                    }
                }

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