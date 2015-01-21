using PokeServer.Interfaces;

namespace PokeServer.Packets.Client.Joined
{
    public struct BattleCreatePacket : IPacket
    {
        public BattleMode BattleMode;
        public IPlayers Players;

        public byte ID { get { return 0x08; } }

        public IPacket ReadPacket(IProtocolDataReader reader)
        {
            BattleMode = (BattleMode) reader.ReadByte();

            switch (BattleMode)
            {
                case BattleMode.PlayerVsNature:
                    Players = new PlayersPlayerVsNature().FromReader(reader);
                    break;
                case BattleMode.PlayerVsNatureDual:
                    Players = new PlayersPlayerVsNatureDual().FromReader(reader);
                    break;
                case BattleMode.PlayerVsBot:
                    Players = new PlayersPlayerVsBot().FromReader(reader);
                    break;
                case BattleMode.PlayerVsBotDual:
                    Players = new PlayersPlayerVsBotDual().FromReader(reader);
                    break;
                case BattleMode.PlayerVsPlayer:
                    Players = new PlayersPlayerVsPlayer().FromReader(reader);
                    break;
                case BattleMode.PlayerVsPlayerDual:
                    Players = new PlayersPlayerVsPlayerDual().FromReader(reader);
                    break;

                case BattleMode.TwoPlayersVsTwoPlayers:
                    Players = new PlayersTwoPlayersVsTwoPlayers().FromReader(reader);
                    break;
            }

            return this;
        }

        public IPacket WritePacket(IProtocolStream stream)
        {
            stream.WriteVarInt(ID);
            stream.WriteByte((byte)BattleMode);
            Players.ToStream(stream);
            stream.Purge();

            return this;
        }
    }
}