using PokeServer.Interfaces;

namespace PokeServer.Packets.Joining
{
    public struct LoginSuccessPacket : IPacket
    {
        public string Usermane;
        public int PlayerID;

        public byte ID { get { return 0x02; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            Usermane = reader.ReadString();
            PlayerID = reader.ReadInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteString(Usermane);
            stream.WriteInt(PlayerID);
            stream.Purge();

            return this;
        }
    }
}
