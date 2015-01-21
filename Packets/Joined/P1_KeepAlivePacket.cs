using PokeServer.Interfaces;

namespace PokeServer.Packets.Joined
{
    public struct KeepAlivePacket : IPacket
    {
        public int KeepAlive;

        public byte ID { get { return 0x01; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            KeepAlive = reader.ReadVarInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteVarInt(KeepAlive);
            stream.Purge();

            return this;
        }
    }
}
