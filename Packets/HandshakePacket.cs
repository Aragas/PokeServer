using Poke.Core.Interfaces;

namespace PokeServer.Packets
{
    public struct HandshakePacket : IPacket
    {
        public int ProtocolVersion;
        public byte ConnectionMode;
        public byte Unused_2;


        public const byte PacketID = 0x00;
        public byte ID { get { return PacketID; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            ProtocolVersion = reader.ReadVarInt();
            ConnectionMode = reader.ReadByte();
            Unused_2 = reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteVarInt(ProtocolVersion);
            stream.WriteByte(ConnectionMode);
            stream.WriteByte(Unused_2);
            stream.Purge();

            return this;
        }
    }

}
