using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct BattleAttackPacket : IPacket
    {
        public byte ID { get { return 0xB1; } }

        public VarInt BattleID { get; set; }
        public TrainerPetMoveMeta Attacker { get; set; }
        public TrainerPetMeta Defender { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            Attacker = TrainerPetMoveMeta.FromReader(reader);
            Defender = TrainerPetMeta.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            Attacker.ToStream(stream);
            Defender.ToStream(stream);

            return this;
        }
    }
}