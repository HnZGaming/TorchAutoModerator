using System;
using AutoModerator.Broadcasts;
using AutoModerator.Core;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Utils.Torch;

namespace AutoModerator.Players
{
    public sealed class PlayerGpsSource : IEntityGpsSource
    {
        public interface IConfig
        {
            string PlayerGpsNameFormat { get; }
            string PlayerGpsDescriptionFormat { get; }
            string GpsColorCode { get; }
        }

        readonly IConfig _config;
        readonly TrackedEntitySnapshot _snapshot;
        readonly long _gridId;
        readonly int _rank;

        public PlayerGpsSource(IConfig config, TrackedEntitySnapshot snapshot, long gridId, int rank)
        {
            _config = config;
            _snapshot = snapshot;
            _gridId = gridId;
            _rank = rank;
        }

        public long GridId => _gridId;

        public bool TryCreateGps(out MyGps gps)
        {
            if (!MySession.Static.Players.TryGetPlayerById(_snapshot.Id, out var player))
            {
                gps = default;
                return false;
            }

            if (!VRageUtils.TryGetCubeGridById(_gridId, out var grid))
            {
                gps = default;
                return false;
            }

            var faction = MySession.Static.Factions.GetPlayerFaction(_snapshot.Id);

            var name = Format(_config.PlayerGpsNameFormat);
            var description = Format(_config.PlayerGpsDescriptionFormat);

            gps = GpsUtils.CreateGridGps(grid, name, description, _config.GpsColorCode);
            return true;

            string Format(string format)
            {
                return format
                    .Replace("{player}", player.DisplayName ?? "<none>")
                    .Replace("{faction}", faction?.Tag ?? "<none>")
                    .Replace("{ratio}", $"{_snapshot.LongLagNormal * 100:0}%")
                    .Replace("{rank}", GpsUtils.RankToString(_rank))
                    .Replace("{time}", GpsUtils.RemainingTimeToString(_snapshot.RemainingTime));
            }
        }
    }
}