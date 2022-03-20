using System.IO;
using Sandbox.ModAPI;
using VRage;

namespace HNZ.LocalGps.Interface
{
    public class LocalGpsApi
    {
        public static readonly long ModVersion = "LocalGpsApi 1.0.*".GetHashCode();

        readonly long _moduleId;

        public LocalGpsApi(long moduleId)
        {
            _moduleId = moduleId;
        }

        public void AddOrUpdateLocalGps(LocalGpsSource src)
        {
            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteAddOrUpdateLocalGps(_moduleId, src);
                MyAPIGateway.Utilities.SendModMessage(ModVersion, stream.Data);
            }
        }

        public void RemoveLocalGps(long gpsId)
        {
            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteRemoveLocalGps(_moduleId, gpsId);
                MyAPIGateway.Utilities.SendModMessage(ModVersion, stream.Data);
            }
        }
    }
}