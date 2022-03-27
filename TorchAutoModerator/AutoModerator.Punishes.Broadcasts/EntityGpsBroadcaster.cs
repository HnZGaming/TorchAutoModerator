using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HNZ.LocalGps.Interface;
using NLog;
using Sandbox.Game.World;
using Torch.Utils;
using Utils.General;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace AutoModerator.Punishes.Broadcasts
{
    public sealed class EntityGpsBroadcaster
    {
        public interface IConfig
        {
            string GpsNameFormat { get; }
            string GpsDescriptionFormat { get; }
            string GpsColorCode { get; }
            IEnumerable<ulong> GpsMutedPlayers { get; }
            MyPromoteLevel GpsVisiblePromoteLevel { get; }
        }

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IConfig _config;
        readonly Dictionary<long, LocalGpsSource> _gpsSources;
        readonly LocalGpsApi _gpsApi;

        public EntityGpsBroadcaster(IConfig config)
        {
            _config = config;
            _gpsSources = new Dictionary<long, LocalGpsSource>();
            _gpsApi = new LocalGpsApi(nameof(EntityGpsBroadcaster).GetHashCode());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<LocalGpsSource> GetGpss()
        {
            return _gpsSources.Values.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearGpss()
        {
            foreach (var (id, _) in _gpsSources)
            {
                _gpsApi.RemoveLocalGps(id);
            }

            _gpsSources.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ReplaceGpss(IEnumerable<GridGpsSource> gpsSources)
        {
            var sources = DictionaryPool<long, LocalGpsSource>.Create();

            foreach (var src in gpsSources)
            {
                if (TryCreateGps(src, out var localGpsSource))
                {
                    sources[localGpsSource.Id] = localGpsSource;
                }
            }

            // remove GPSs that don't exist anymore
            foreach (var (existingGpsId, _) in _gpsSources.ToArray())
            {
                if (!sources.ContainsKey(existingGpsId))
                {
                    _gpsSources.Remove(existingGpsId);
                    _gpsApi.RemoveLocalGps(existingGpsId);
                }
            }

            // add/update other GPSs
            foreach (var (id, src) in sources)
            {
                _gpsSources[id] = src;
                _gpsApi.AddOrUpdateLocalGps(src);
            }

            DictionaryPool<long, LocalGpsSource>.Release(sources);
        }

        bool TryCreateGps(GridGpsSource source, out LocalGpsSource gps)
        {
            if (!VRageUtils.TryGetCubeGridById(source.GridId, out var grid))
            {
                Log.Trace($"broadcast: grid not found: {source.GridId}");
                gps = default;
                return false;
            }

            var playerName = (string)null;

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

            gps = new LocalGpsSource
            {
                Id = grid.EntityId,
                Name = name,
                Color = ColorUtils.TranslateColor(_config.GpsColorCode),
                Description = description,
                Position = grid.PositionComp.GetPosition(),
                Radius = 0,
                EntityId = grid.EntityId,
                PromoteLevel = (int)_config.GpsVisiblePromoteLevel,
                ExcludedPlayers = _config.GpsMutedPlayers.ToArray(),
            };

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