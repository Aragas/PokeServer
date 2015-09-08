using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Client.Joined
{
    public struct BattleUseItemPacket : IPacket
    {
        public byte ID { get { return 0xB2; } }

        public VarInt BattleID { get; set; }
        public TrainerPetMeta ItemUser { get; set; }
        public VarInt Item { get; set; }


        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            ItemUser = TrainerPetMeta.FromReader(reader);
            Item = reader.ReadVarInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            ItemUser.ToStream(stream);
            stream.WriteVarInt(Item);

            return this;
        }
    }
}