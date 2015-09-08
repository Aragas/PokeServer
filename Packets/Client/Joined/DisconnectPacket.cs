using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct DisconnectPacket : IPacket
    {
        public string Reason;

        public byte ID { get { return 0xFF; }
        }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Reason = reader.ReadString();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteString(Reason);
            stream.Purge();

            return this;
        }
    }
}
