using System;
using ProtoBuf;
using VRage.Game.ModAPI;
using VRageMath;

namespace HNZ.LocalGps.Interface
{
    [Serializable]
    [ProtoContract]
    public sealed class LocalGpsSource
    {
        [ProtoMember(1)]
        public long Id { get; set; }

        [ProtoMember(2)]
        public string Name { get; set; }

        [ProtoMember(3)]
        public Color Color { get; set; }

        [ProtoMember(4)]
        public string Description { get; set; }

        [ProtoMember(5)]
        public Vector3D Position { get; set; }

        [ProtoMember(6, IsRequired = false)]
        public double Radius { get; set; } // less than 0 inclusive -> everyone

        [ProtoMember(7, IsRequired = false)]
        public long EntityId { get; set; }

        [ProtoMember(8, IsRequired = false)]
        public int PromoteLevel { get; set; }

        [ProtoMember(9, IsRequired = false)]
        public ulong[] ExcludedPlayers { get; set; }

        public override string ToString()
        {
            return $"{nameof(Id)}: {Id}, {nameof(Name)}: {Name}, {nameof(Color)}: {Color}, {nameof(Description)}: {Description}, {nameof(Position)}: {Position}, {nameof(Radius)}: {Radius}, {nameof(EntityId)}: {EntityId}";
        }
    }
}