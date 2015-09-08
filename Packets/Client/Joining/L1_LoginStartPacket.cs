using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joining
{
    public struct LoginStartPacket : IPacket
    {
        public string Name;

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Name = reader.ReadString();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteString(Name);
            stream.Purge();

            return this;
        }
    }
}
