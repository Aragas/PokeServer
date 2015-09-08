using Poke.Core.Interfaces;

namespace PokeServer.Packets.Joined
{
    public enum BattleCreateStatus : byte { Success, Error }

    public struct BattleEnteringBattlePacket : IPacket
    {
        public int BattleID;

        public byte ID { get { return 0x00; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.Purge();

            return this;
        }
    }
}