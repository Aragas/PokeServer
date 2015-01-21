using PokeServer.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleSwitchPokemonPacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public OpponentBattle Switcher;
        public PokemonBattle Pokemon;
        public PokemonToSwitch SwitchPokemon;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            Switcher = (OpponentBattle) reader.ReadByte();
            Pokemon = (PokemonBattle) reader.ReadByte();
            SwitchPokemon = (PokemonToSwitch) reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.WriteByte((byte) Switcher);
            stream.WriteByte((byte) Pokemon);
            stream.WriteByte((byte) SwitchPokemon);
            stream.Purge();

            return this;
        }
    }
}