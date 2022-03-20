using System.IO;
using Sandbox.ModAPI;

namespace HNZ.LocalGps.Interface
{
    public static class LocalGpsSourceBinary
    {
        public static void WriteAddOrUpdateLocalGps(this BinaryWriter writer, long moduleId, LocalGpsSource src)
        {
            writer.Write(true);
            writer.Write(moduleId);
            writer.WriteProtobuf(src);
        }

        public static void WriteRemoveLocalGps(this BinaryWriter writer, long moduleId, long gpsId)
        {
            writer.Write(false);
            writer.Write(moduleId);
            writer.Write(gpsId);
        }

        public static void ReadLocalGps(this BinaryReader reader, out bool isAddOrUpdate, out long moduleId, out LocalGpsSource source, out long gpsId)
        {
            if (reader.ReadBoolean())
            {
                isAddOrUpdate = true;
                moduleId = reader.ReadInt64();
                source = reader.ReadProtobuf<LocalGpsSource>();
                gpsId = source.Id;
            }
            else
            {
                isAddOrUpdate = false;
                moduleId = reader.ReadInt64();
                gpsId = reader.ReadInt64();
                source = null;
            }
        }

        static T ReadProtobuf<T>(this BinaryReader self)
        {
            var length = self.ReadInt32();
            var load = self.ReadBytes(length);
            var content = MyAPIGateway.Utilities.SerializeFromBinary<T>(load);
            return content;
        }

        static void WriteProtobuf<T>(this BinaryWriter self, T content)
        {
            var load = MyAPIGateway.Utilities.SerializeToBinary(content);
            self.Write(load.Length);
            self.Write(load);
        }
    }
}