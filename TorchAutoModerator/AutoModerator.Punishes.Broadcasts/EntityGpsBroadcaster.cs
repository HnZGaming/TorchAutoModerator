using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using TorchEntityGpsBroadcaster.Core;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Punishes.Broadcasts
{
    public sealed class EntityGpsBroadcaster
    {
        public interface IConfig
        {
            string GpsNameFormat { get; }
            string GpsDescriptionFormat { get; }
            string GpsColorCode { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly EntityIdGpsCollection _gpsCollection;

        public EntityGpsBroadcaster(IConfig config)
        {
            _config = config;
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
            IEnumerable<GridGpsSource> gpsSources,
            IEnumerable<long> receiverIdentityIds,
            CancellationToken canceller)
        {
            var gpss = await CreateGpss(gpsSources, canceller);
            _gpsCollection.SendReplaceAllTrackedGpss(gpss, receiverIdentityIds);
        }

        async Task<IReadOnlyList<MyGps>> CreateGpss(
            IEnumerable<GridGpsSource> gpsSources,
            CancellationToken canceller)
        {
            try
            {
                // MyGps can be created in the game loop only (idk why)
                await GameLoopObserver.MoveToGameLoop(canceller);

                var stopwatch = Stopwatch.StartNew();

                var gpss = new List<MyGps>();
                foreach (var gpsSource in gpsSources)
                {
                    if (TryCreateGps(gpsSource, out var gps))
                    {
                        gpss.Add(gps);
                    }
                }

                var timeSpent = stopwatch.ElapsedMilliseconds;
                stopwatch.Stop();

                Log.Trace($"Creating GPSs time spent: {timeSpent:0.00}ms");

                return gpss;
            }
            finally
            {
                // make sure we're out of the main thread
                await TaskUtils.MoveToThreadPool(canceller);
            }
        }

        bool TryCreateGps(GridGpsSource source, out MyGps gps)
        {
            if (!VRageUtils.TryGetCubeGridById(source.GridId, out var grid))
            {
                gps = default;
                return false;
            }

            var playerName = (string) null;

            if (!grid.BigOwners.TryGetFirst(out var playerId))
            {
                Log.Trace($"grid no owner: \"{grid.DisplayName}\"");
            }
            else if (!MySession.Static.Players.TryGetPlayerById(playerId, out var player))
            {
                Log.Trace($"player not found for grid: \"{grid.DisplayName}\": {playerId}");
            }
            else
            {
                playerName = player.DisplayName;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var factionTag = faction?.Tag;

            var name = Format(_config.GpsNameFormat);
            var description = Format(_config.GpsDescriptionFormat);
            gps = GpsUtils.CreateGridGps(grid, name, description, _config.GpsColorCode);
            return true;

            string Format(string format)
            {
                return format
                    .Replace("{grid}", grid.DisplayName)
                    .Replace("{player}", playerName ?? "<none>")
                    .Replace("{faction}", factionTag ?? "<none>")
                    .Replace("{ratio}", $"{source.LongLagNormal * 100:0}%")
                    .Replace("{rank}", GpsUtils.RankToString(source.Rank))
                    .Replace("{time}", GpsUtils.RemainingTimeToString(source.RemainingTime));
            }
        }
    }
}