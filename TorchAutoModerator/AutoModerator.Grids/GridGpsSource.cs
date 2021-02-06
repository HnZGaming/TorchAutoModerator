using System;
using AutoModerator.Broadcasts;
using AutoModerator.Core;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Utils.General;
using Utils.Torch;

namespace AutoModerator.Grids
{
    // this class shouldn't hold onto any game entities so it won't mess with the game's GC
    public sealed class GridGpsSource : IEntityGpsSource
    {
        public interface IConfig
        {
            string GridGpsNameFormat { get; }
            string GridGpsDescriptionFormat { get; }
            string GpsColorCode { get; }
        }

        readonly IConfig _config;
        readonly TrackedEntitySnapshot _snapshot;
        readonly int _rank;

        public GridGpsSource(IConfig config, TrackedEntitySnapshot snapshot, int rank)
        {
            _config = config;
            _snapshot = snapshot;
            _rank = rank;
        }

        public long GridId => _snapshot.EntityId;

        public bool TryCreateGps(out MyGps gps)
        {
            if (!VRageUtils.TryGetCubeGridById(_snapshot.EntityId, out var grid))
            {
                gps = default;
                return false;
            }

            var playerName = (string) null;

            if (grid.BigOwners.TryGetFirst(out var playerId) &&
                MySession.Static.Players.TryGetPlayerById(playerId, out var player))
            {
                playerName = player.DisplayName;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            var factionTag = faction?.Tag;

            var name = Format(_config.GridGpsNameFormat);
            var description = Format(_config.GridGpsDescriptionFormat);
            gps = GpsUtils.CreateGridGps(grid, name, description, _config.GpsColorCode);
            return true;

            string Format(string format)
            {
                return format
                    .Replace("{grid}", grid.DisplayName)
                    .Replace("{player}", playerName ?? "<none>")
                    .Replace("{faction}", factionTag ?? "<none>")
                    .Replace("{ratio}", $"{_snapshot.LongLagNormal * 100:0}%")
                    .Replace("{rank}", GpsUtils.RankToString(_rank))
                    .Replace("{time}", GpsUtils.RemainingTimeToString(_snapshot.RemainingTime));
            }
        }
    }
}