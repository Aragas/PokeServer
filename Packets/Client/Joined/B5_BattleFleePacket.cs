using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleFleePacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public OpponentBattle User;
        public PokemonBattle Pokemon;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            User = (OpponentBattle) reader.ReadByte();
            Pokemon = (PokemonBattle) reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.WriteByte((byte) User);
            stream.WriteByte((byte) Pokemon);
            stream.Purge();

            return this;
        }
    }
}