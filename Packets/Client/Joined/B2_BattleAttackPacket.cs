using PokeServer.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleAttackPacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public OpponentBattle OpponentAttacker;  // 5 bits - opponents, 3 - pokemon
        public OpponentBattle OpponentDefender;
        public PokemonBattle PokemonAttacker;
        public PokemonBattle PokemonDefender;
        public MoveBattle Move;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            OpponentAttacker = (OpponentBattle) reader.ReadByte();
            OpponentDefender = (OpponentBattle) reader.ReadByte();
            PokemonAttacker = (PokemonBattle) reader.ReadByte();
            PokemonDefender = (PokemonBattle) reader.ReadByte();
            Move = (MoveBattle) reader.ReadShort();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.WriteByte((byte) OpponentAttacker);
            stream.WriteByte((byte) OpponentDefender);
            stream.WriteByte((byte) PokemonAttacker);
            stream.WriteByte((byte) PokemonDefender);
            stream.WriteShort((short) Move);
            stream.Purge();

            return this;
        }
    }
}