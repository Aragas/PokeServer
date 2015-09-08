using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct BattleSwitchPokemonPacket : IPacket
    {
        public byte ID { get { return 0xB3; } }

        public VarInt BattleID { get; set; }
        public TrainerPetMeta Switcher { get; set; }
        public VarInt SwitchPet { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            Switcher = TrainerPetMeta.FromReader(reader);
            SwitchPet = reader.ReadVarInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            Switcher.ToStream(stream);
            stream.WriteVarInt(SwitchPet);

            return this;
        }
    }
}