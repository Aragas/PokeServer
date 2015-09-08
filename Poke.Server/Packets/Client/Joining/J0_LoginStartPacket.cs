using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joining
{
    public struct LoginStartPacket : IPacket
    {
        public string Name { get; set; }

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Name = reader.ReadString();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteString(Name);

            return this;
        }
    }
}
