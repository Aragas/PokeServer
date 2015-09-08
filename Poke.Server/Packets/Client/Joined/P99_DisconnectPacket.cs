using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct DisconnectPacket : IPacket
    {
        public byte ID { get { return 0xFF; } }

        public string Reason { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Reason = reader.ReadString();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteString(Reason);

            return this;
        }
    }
}
