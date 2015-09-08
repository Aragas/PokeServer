using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct JoinGamePacket : IPacket
    {
        public int PlayerID;
        public Player Player;
        
        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            PlayerID = reader.ReadInt();
            Player = Player.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(PlayerID);
            Player.ToStream(stream);
            stream.Purge();

            return this;
        }
    }
}
