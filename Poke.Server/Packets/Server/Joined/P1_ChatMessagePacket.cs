using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Server.Joined
{
    public struct ChatMessagePacket : IPacket
    {
        public string Message { get; set; }

        public byte ID { get { return 0x01; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Message = reader.ReadString();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteString(Message);

            return this;
        }
    }
}
