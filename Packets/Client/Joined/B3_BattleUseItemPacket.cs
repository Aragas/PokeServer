using PokeServer.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleUseItemPacket : IPacket
    {
        public byte ID { get { return 0x00; } }

        public int BattleID;
        public OpponentBattle ItemUser;
        public PokemonBattle Pokemon;
        public Item Item;

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleID = reader.ReadInt();
            ItemUser = (OpponentBattle) reader.ReadByte();
            Pokemon = (PokemonBattle) reader.ReadByte();
            Item = Item.FromReader(reader);

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteInt(BattleID);
            stream.WriteByte((byte) ItemUser);
            stream.WriteByte((byte) Pokemon);
            Item.ToStream(stream);
            stream.Purge();

            return this;
        }
    }
}