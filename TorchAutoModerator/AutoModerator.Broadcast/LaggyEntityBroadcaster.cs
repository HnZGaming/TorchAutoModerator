using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Screens.Helpers;
using TorchEntityGpsBroadcaster.Core;

namespace AutoModerator.Broadcast
{
    public sealed class LaggyEntityBroadcaster
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly EntityIdGpsCollection _gpsCollection;
        readonly BroadcastListenerCollection _gpsReceivers;
        readonly Dictionary<long, IEntityGpsSource> _allGpsSources;
        readonly EntityGpsCreator _entityGpsCreator;

        public LaggyEntityBroadcaster(BroadcastListenerCollection gpsReceivers)
        {
            _gpsReceivers = gpsReceivers;
            _gpsCollection = new EntityIdGpsCollection("<!> ");
            _allGpsSources = new Dictionary<long, IEntityGpsSource>();
            _entityGpsCreator = new EntityGpsCreator();
        }

        public IEnumerable<MyGps> GetAllGpss()
        {
            return _gpsCollection.GetAllTrackedGpss();
        }

        public void ClearAllGpss()
        {
            _gpsCollection.SendDeleteAllGpss();
        }

        public void InitializeInterval()
        {
            _allGpsSources.Clear();
        }

        public void AddGpsSourceRange(IEnumerable<IEntityGpsSource> gpsSources)
        {
            foreach (var gpsSource in gpsSources)
            {
                _allGpsSources[gpsSource.AttachedEntityId] = gpsSource;
            }
        }

        public async Task SendIntervalGpss(int maxGpsCount, CancellationToken canceller)
        {
            var broadcastableGpsSources = _allGpsSources
                .Values
                .OrderByDescending(s => s.LagNormal)
                .Take(maxGpsCount);

            var gpss = await _entityGpsCreator.Create(broadcastableGpsSources, canceller);
            var targetIds = _gpsReceivers.GetReceiverIdentityIds();
            _gpsCollection.SendReplaceAllTrackedGpss(gpss, targetIds);
            _allGpsSources.Clear();

            Log.Debug($"broadcasted {gpss.Count} laggy entities");
        }
    }
}