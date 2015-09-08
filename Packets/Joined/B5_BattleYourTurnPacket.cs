using Poke.Core.Interfaces;

namespace PokeServer.Packets.Joined
{
    public struct BattleYourTurnPacket : IPacket
    {
        public int BattleID;
        public Battle Battle;

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            Battle = Battle.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            Battle.ToStream(stream);
            stream.Purge();

            return this;
        }
    }
}