using System;
using AutoModerator.Broadcast;
using AutoModerator.Core;
using Sandbox.Game.Screens.Helpers;
using Torch.Utils;

namespace AutoModerator.Players
{
    public sealed class PlayerGpsSource : IEntityGpsSource
    {
        public interface IConfig
        {
            string PlayerGpsNameFormat { get; }
            string PlayerGpsDescriptionFormat { get; }
            string GpsColor { get; }
        }

        readonly IConfig _config;
        readonly TimeSpan _remainingTime;
        readonly int _rank;
        readonly long _playerId;
        readonly string _playerNameOrNull;
        readonly string _factionTagOrNull;

        public PlayerGpsSource(IConfig config, PlayerLagSnapshot snapshot, TimeSpan remainingTime, int rank)
        {
            _config = config;
            _remainingTime = remainingTime;
            _rank = rank;
            AttachedEntityId = snapshot.SignatureGridId; //note
            LagNormal = snapshot.LagNormal;
            _playerId = snapshot.EntityId;
            _playerNameOrNull = snapshot.PlayerName;
            _factionTagOrNull = snapshot.FactionTagOrNull;
        }

        public long AttachedEntityId { get; }
        public double LagNormal { get; }

        public bool TryCreateGps(out MyGps gps)
        {
            if (GpsUtils.TryGetGridById(AttachedEntityId, out var grid))
            {
                var name = ToString(_config.PlayerGpsNameFormat);
                var description = ToString(_config.PlayerGpsDescriptionFormat);
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
            return $"[{factionTag}] {playerName} ({_playerId}) {normal} ({rank}) {remainingTime}";
        }
    }
}