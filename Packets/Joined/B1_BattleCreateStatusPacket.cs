using Poke.Core.Interfaces;

namespace PokeServer.Packets.Joined
{
    public struct BattleCreateStatusPacket : IPacket
    {
        public int BattleID;
        public BattleCreateStatus Status;

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            Status = (BattleCreateStatus) reader.ReadByte();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.WriteByte((byte) Status);
            stream.Purge();

            return this;
        }
    }
}