using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Server.Joined
{
    public struct BattleYourTurnPacket : IPacket
    {
        public VarInt BattleID { get; set; }
        public Battle Battle { get; set; }

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            Battle = Battle.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            Battle.ToStream(stream);

            return this;
        }
    }
}