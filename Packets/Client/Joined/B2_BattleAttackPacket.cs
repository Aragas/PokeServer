using Poke.Core.Data;
using Poke.Core.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleAttackPacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public TrainerBattleMeta Attacker;  // 3 bits - opponents, 3 - pet
        public TrainerBattleMeta Defender;  // 3 bits - opponents, 3 - pet
        public MoveBattle Move;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            Attacker = TrainerBattleMeta.FromReader(reader);
            Defender = TrainerBattleMeta.FromReader(reader);
            Move = (MoveBattle) reader.ReadShort();

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            Attacker.ToStreamByte(stream);
            Defender.ToStreamByte(stream);
            stream.WriteShort((short) Move);
            stream.Purge();

            return this;
        }
    }
}