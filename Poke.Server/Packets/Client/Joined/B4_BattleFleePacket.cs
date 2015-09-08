using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct BattleFleePacket : IPacket
    {
        public byte ID { get { return 0xB4; } }

        public VarInt BattleID { get; set; }
        public TrainerPetMeta Pussy { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            Pussy = TrainerPetMeta.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            Pussy.ToStream(stream);

            return this;
        }
    }
}