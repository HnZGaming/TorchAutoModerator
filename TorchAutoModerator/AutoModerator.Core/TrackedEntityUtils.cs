using System;
using System.Collections.Generic;
using System.Linq;
using Utils.General;
using Utils.TimeSerieses;

namespace AutoModerator.Core
{
    public static class TrackedEntityUtils
    {
        public static readonly Func<TrackedEntity, double> GetLongLagNormal = e => e.LagNormal;

        public static IEnumerable<TrackedEntity> GetLaggiestEntities(this IReadOnlyDictionary<long, TrackedEntity> self, bool pinnedOnly = false)
        {
            return self
                .Values
                .Where(e => !pinnedOnly || e.IsPinned)
                .OrderByDescending(GetLongLagNormal);
        }

        public static bool TryFindEntityByName(this IReadOnlyDictionary<long, TrackedEntity> self, string name, out TrackedEntity entity)
        {
            return self.Values.TryGetFirst(e => e.Name == name, out entity);
        }

        public static bool TryGetTimeSeries(this IReadOnlyDictionary<long, TrackedEntity> self, long id, out ITimeSeries<double> timeSeries)
        {
            if (self.TryGetValue(id, out var entity))
            {
                timeSeries = entity.TimeSeries;
                return true;
            }

            timeSeries = default;
            return true;
        }
    }
}