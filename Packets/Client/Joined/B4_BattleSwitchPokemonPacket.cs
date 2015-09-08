using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleSwitchPokemonPacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public TrainerBattleMeta Switcher;
        public PokemonToSwitch SwitchPokemon;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            Switcher = TrainerBattleMeta.FromReader(reader);
            SwitchPokemon = (PokemonToSwitch) reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            Switcher.ToStreamByte(stream);
            stream.WriteByte((byte) SwitchPokemon);
            stream.Purge();

            return this;
        }
    }
}