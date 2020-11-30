using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Torch.Utils;

namespace TorchShittyShitShitter.Reflections
{
    public static class MyGpsCollection_PlayerGpss
    {
#pragma warning disable 649
        [ReflectedFieldInfo(typeof(MyGpsCollection), "m_playerGpss")]
        static readonly FieldInfo _fieldInfo;
#pragma warning restore 649

        public static Dictionary<long, Dictionary<int, MyGps>> GetPlayerGpss(this MyGpsCollection self)
        {
            return (Dictionary<long, Dictionary<int, MyGps>>) _fieldInfo.GetValue(self);
        }

        public static void DeleteWhere(this MyGpsCollection self, Func<MyGps, bool> f)
        {
            var worldGpsCollection = self.GetPlayerGpss();
            var removedGpsList = new List<(long, int)>();
            foreach (var (identity, gpsCollection) in worldGpsCollection)
            foreach (var (_, gps) in gpsCollection)
            {
                if (f(gps))
                {
                    removedGpsList.Add((identity, gps.Hash));
                }
            }

            foreach (var (identityId, gpsHash) in removedGpsList)
            {
                self.SendDelete(identityId, gpsHash);
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