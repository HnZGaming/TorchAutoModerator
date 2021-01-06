using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Torch.Utils;

namespace EntityGpsBroadcasters.Core
{
    internal static class MyGpsCollection_PlayerGpss
    {
#pragma warning disable 649
        [ReflectedFieldInfo(typeof(MyGpsCollection), "m_playerGpss")]
        static readonly FieldInfo _fieldInfo;
#pragma warning restore 649

        public static Dictionary<long, Dictionary<int, MyGps>> GetPlayerGpss(this MyGpsCollection self)
        {
            return (Dictionary<long, Dictionary<int, MyGps>>) _fieldInfo.GetValue(self);
        }

        public static IEnumerable<(long IdentityId, MyGps Gps)> Where(
            this MyGpsCollection self, Func<long, MyGps, bool> f)
        {
            var result = new List<(long, MyGps)>();
            var worldGpsCollection = self.GetPlayerGpss();
            foreach (var (identity, gpsCollection) in worldGpsCollection)
            foreach (var (_, gps) in gpsCollection)
            {
                if (f(identity, gps))
                {
                    result.Add((identity, gps));
                }
            }

            return result;
        }

        public static void DeleteWhere(this MyGpsCollection self, Func<long, MyGps, bool> f)
        {
            foreach (var (identityId, gps) in self.Where(f))
            {
                self.SendDelete(identityId, gps.Hash);
            }
        }

        public static void SendAddOrModify(
            this MyGpsCollection self,
            IEnumerable<long> identityIds,
            MyGps gps,
            long? entityId)
        {
            var worldGpsCollection = self.GetPlayerGpss();
            foreach (var identityId in identityIds)
            {
                if (worldGpsCollection.TryGetValue(identityId, out var gpsCollection))
                {
                    if (gpsCollection.ContainsKey(gps.Hash))
                    {
                        self.SendModifyGps(identityId, gps);
                        continue;
                    }
                }

                self.SendAddGps(identityId, ref gps, entityId ?? 0L);
            }
        }
    }
}