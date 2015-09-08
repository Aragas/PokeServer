using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace Poke.Server.Packets.Server.Joined
{
    public struct BattleNotFoundPacket : IPacket
    {
        public VarInt BattleID { get; set; }

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadVarInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(BattleID);

            return this;
        }
    }
}