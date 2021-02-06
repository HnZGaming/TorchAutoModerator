using System;
using AutoModerator.Broadcast;
using AutoModerator.Core;
using Sandbox.Game.Screens.Helpers;
using Torch.Utils;

namespace AutoModerator.Grids
{
    // this class shouldn't hold onto any game entities so it won't mess with the game's GC
    public sealed class GridGpsSource : IEntityGpsSource
    {
        public interface IConfig
        {
            string GridGpsNameFormat { get; }
            string GridGpsDescriptionFormat { get; }
            string GpsColor { get; }
        }

        readonly IConfig _config;
        readonly string _gridName;
        readonly string _factionTagOrNull;
        readonly string _playerNameOrNull;
        readonly TimeSpan _remainingTime;
        readonly int _rank;

        public GridGpsSource(IConfig config, GridLagSnapshot snapshot, TimeSpan remainingTime, int rank)
        {
            _config = config;
            AttachedEntityId = snapshot.EntityId;
            LagNormal = snapshot.LagNormal;
            _gridName = snapshot.GridName;
            _factionTagOrNull = snapshot.FactionTagOrNull;
            _playerNameOrNull = snapshot.PlayerNameOrNull;
            _remainingTime = remainingTime;
            _rank = rank;
        }

        public long AttachedEntityId { get; }
        public double LagNormal { get; }

        public bool TryCreateGps(out MyGps gps)
        {
            if (GpsUtils.TryGetGridById(AttachedEntityId, out var grid))
            {
                var name = ToString(_config.GridGpsNameFormat);
                var description = ToString(_config.GridGpsDescriptionFormat);
                var color = ColorUtils.TranslateColor(_config.GpsColor);
                gps = GpsUtils.CreateGridGps(grid, name, description, color);
                return true;
            }

            gps = null;
            return false;
        }

        string ToString(string format)
        {
            return format
                .Replace("{grid}", _gridName)
                .Replace("{player}", _playerNameOrNull ?? "<none>")
                .Replace("{faction}", _factionTagOrNull ?? "<none>")
                .Replace("{ratio}", $"{LagNormal * 100:0}%")
                .Replace("{rank}", GpsUtils.RankToString(_rank))
                .Replace("{time}", GpsUtils.RemainingTimeToString(_remainingTime));
        }

        public override string ToString()
        {
            var normal = $"{LagNormal * 100f:0.00}%";
            var remainingTime = $"{_remainingTime.TotalMinutes:0.0}m";
            var factionTag = _factionTagOrNull ?? "<single>";
            var playerName = _playerNameOrNull ?? "<none>";
            var rank = GpsUtils.RankToString(_rank);
            return $"\"{_gridName}\" ({AttachedEntityId}) [{factionTag}] {playerName}{normal} ({rank}) {remainingTime}";
        }
    }
}