using System;
using NLog;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Core
{
    public sealed class TrackedEntity
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        readonly EntityTracker.IConfig _config;
        readonly TimeSeries<double> _timeSeries;
        readonly DateTime _spawnTime;
        DateTime? _pinExpirationTime;

        public TrackedEntity(EntityTracker.IConfig config, long id)
        {
            Id = id;
            _config = config;
            _timeSeries = new TimeSeries<double> { { DateTime.UtcNow, 0 } };
            _spawnTime = DateTime.UtcNow;
        }

        // some properties serve "snapshot" purposes because
        // MyEntities.GetEntityByName() is not thread-safe

        public long Id { get; }
        public string Name { get; private set; }
        public long OwnerId { get; private set; } //todo multiple owners
        public string OwnerName { get; private set; } //todo multiple owners
        public string FactionTag { get; private set; } //todo multiple owners
        public bool IsBlessed { get; private set; }
        public bool IsPinned => _pinExpirationTime > DateTime.UtcNow;
        public double LatestMspf { get; private set; }
        public double LagNormal { get; private set; }
        public ITimeSeries<double> TimeSeries => _timeSeries;
        public TimeSpan PinRemainingTime => (_pinExpirationTime - DateTime.UtcNow) ?? TimeSpan.Zero;

        public void Update(in EntitySource? srcOrNull)
        {
            // clean up old data
            var now = DateTime.UtcNow;
            _timeSeries.Retain(now - _config.TrackingSpan); //todo optimize

            if (srcOrNull is { } src)
            {
                if (src.EntityId != Id)
                {
                    throw new InvalidOperationException("wrong entity");
                }

                Name = src.Name ?? $"<{Id}>";
                OwnerId = src.OwnerId;
                OwnerName = src.OwnerName ?? $"<{src.OwnerId}>";
                FactionTag = src.FactionTag;
                LatestMspf = src.LagMspf;

                var lagNormal = src.LagMspf / _config.PinLag;
                _timeSeries.Add(now, lagNormal);
                Log.Trace($"input: {src} -> {lagNormal * 100:0}%");
            }
            else
            {
                _timeSeries.Add(now, 0);
            }

            // ignore first N seconds into existence of an entity (grace period)
            // because spawning (and un-concealing) takes a lot of frame rate
            // NOTE the reason why we're not using the time series data for the "first timestamp"
            // is because we constantly dispose of old elements from the time series.
            var gracePeriodEndTime = _spawnTime + _config.GracePeriodSpan;
            if (gracePeriodEndTime > now) // grace period!
            {
                LagNormal = 0;
                IsBlessed = true;
                _pinExpirationTime = null;
                return;
            }

            LagNormal = CalcLagNormal();

            // don't evaluate until sufficient data is fed (but warning can be issued)
            var minDataTimeLength = _config.TrackingSpan - 5.Seconds(); //todo use capacity instead
            if (!_timeSeries.IsLongerThan(minDataTimeLength))
            {
                IsBlessed = true;
                _pinExpirationTime = null;
                return;
            }

            IsBlessed = false;

            // pin long-laggy entities
            if (LagNormal >= 1)
            {
                _pinExpirationTime = now + _config.PinSpan;
            }
        }

        // returned value of 1+ will (generally) pin given entity for punishment
        double CalcLagNormal()
        {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (_timeSeries.Count == 1)
            {
                return _timeSeries[0].Element; // for outlier test
            }

            var totalNormal = 0d;
            var outlierTests = _timeSeries.TestOutlier();
            for (var i = 0; i < _timeSeries.Count; i++)
            {
                var (_, normal) = _timeSeries[i];
                var outlierTest = outlierTests[i];

                if (_config.OutlierFenceNormal > 0) // switch
                {
                    // prevent catching server hiccups
                    if (outlierTest > _config.OutlierFenceNormal)
                    {
                        totalNormal += Math.Min(1, normal);
                        continue;
                    }
                }

                totalNormal += normal;
            }

            var avgNormal = totalNormal / _timeSeries.Count;
            return avgNormal;
        }

        public override string ToString()
        {
            var currentMspf = $"{LatestMspf:0.00}ms/f";
            var tsCount = TimeSeries.Count;
            var pin = IsPinned ? $"pin: {PinRemainingTime.TotalSeconds:0}secs" : "no-pin";
            return $"tracking: \"{Name}\" ({Id}) -> {currentMspf} {LagNormal * 100:0}% ({tsCount}) {pin}";
        }
    }
}