using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct JoinGamePacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int PlayerID { get; set; }
        public Player Player { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            PlayerID = reader.ReadInt();
            Player = Player.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteInt(PlayerID);
            Player.ToStream(stream);

            return this;
        }
    }
}
