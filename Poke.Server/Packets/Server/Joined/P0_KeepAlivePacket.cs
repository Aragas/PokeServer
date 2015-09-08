using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Server.Joined
{
    public struct KeepAlivePacket : IPacket
    {
        public int KeepAlive { get; set; }

        public byte ID { get { return 0x01; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            KeepAlive = reader.ReadVarInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(KeepAlive);

            return this;
        }
    }
}
