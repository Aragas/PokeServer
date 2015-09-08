using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Server.Joined
{
    public struct BattleCreateStatusPacket : IPacket
    {
        public VarInt BattleID { get; set; }
        public BattleCreateStatus Status { get; set; }

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();
            Status = (BattleCreateStatus) reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);
            stream.WriteByte((byte) Status);

            return this;
        }
    }
}