using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace TorchEntityGpsBroadcaster.Core
{
    public sealed class PrefixedGpsCollection
    {
        readonly string _prefix;

        public PrefixedGpsCollection(string prefix)
        {
            _prefix = prefix;
        }

        static MyGpsCollection Native => MySession.Static.Gpss;

        // we use DisplayName because players can't manipulate/fake it in any way
        // but it takes up space so you should keep the prefix very short
        bool IsOurs(in MyGps g) => g.DisplayName.StartsWith(_prefix);
        void MarkOurs(ref MyGps g) => g.DisplayName = $"{_prefix}{g.DisplayName}";

        public IEnumerable<(long IdentityId, MyGps Gps)> GetAllGpss()
        {
            foreach (var (identityId, gps) in Native.GetAllGpss())
            {
                if (IsOurs(gps))
                {
                    yield return (identityId, gps);
                }
            }
        }

        public IEnumerable<MyGps> GetPlayerGpss(long identityId)
        {
            var gpss = new List<IMyGps>();
            Native.GetGpsList(identityId, gpss);
            return gpss.Cast<MyGps>().Where(g => IsOurs(g));
        }

        public void SendAddGps(long identityId, MyGps gps, bool playSound)
        {
            MarkOurs(ref gps);
            Native.SendAddGps(identityId, ref gps, gps.EntityId, playSound);
        }

        public void SendDeleteGps(long identityId, int gpsHash)
        {
            var gps = Native.GetGps(gpsHash);
            if (!IsOurs(gps))
            {
                throw new Exception($"not ours: {gps.DisplayName}");
            }

            Native.SendDelete(identityId, gpsHash);
        }
    }
}