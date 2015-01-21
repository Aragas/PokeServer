namespace PokeServer.Interfaces
{
    public interface IPlayers
    {
        IPlayers FromReader(IProtocolDataReader reader);
        void ToStream(IProtocolStream stream);
    }

    public class PlayersPlayerVsNature : IPlayers
    {
        public int Player_1;
        public int Player_2;

        public BattleMode Mode = BattleMode.PlayerVsNature;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Player_2 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Player_2);
        }
    }
    public class PlayersPlayerVsNatureDual : IPlayers
    {
        public int Player_1;
        public int Player_2;

        public BattleMode Mode = BattleMode.PlayerVsNatureDual;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Player_2 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Player_2);
        }
    }
    public class PlayersPlayerVsBot : IPlayers
    {
        public int Player_1;
        public int Bot_1;

        public BattleMode Mode = BattleMode.PlayerVsBotDual;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Bot_1 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Bot_1);
        }
    }
    public class PlayersPlayerVsBotDual : IPlayers
    {
        public int Player_1;
        public int Bot_1;

        public BattleMode Mode = BattleMode.PlayerVsBotDual;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Bot_1 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Bot_1);
        }
    }
    public class PlayersPlayerVsPlayer : IPlayers
    {
        public int Player_1;
        public int Player_2;

        public BattleMode Mode = BattleMode.PlayerVsPlayer;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Player_2 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Player_2);
        }
    }
    public class PlayersPlayerVsPlayerDual : IPlayers
    {
        public int Player_1;
        public int Player_2;

        public BattleMode Mode = BattleMode.PlayerVsPlayerDual;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Player_2 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Player_2);
        }
    }
    public class PlayersTwoPlayersVsTwoPlayers : IPlayers
    {
        public int Player_1 { get; private set; }
        public int Player_2 { get; private set; }
        public int Player_3 { get; private set; }
        public int Player_4 { get; private set; }

        public BattleMode Mode = BattleMode.TwoPlayersVsTwoPlayers;

        public IPlayers FromReader(IProtocolDataReader reader)
        {
            Player_1 = reader.ReadInt();
            Player_2 = reader.ReadInt();
            Player_3 = reader.ReadInt();
            Player_4 = reader.ReadInt();

            return this;
        }

        public void ToStream(IProtocolStream stream)
        {
            stream.WriteInt(Player_1);
            stream.WriteInt(Player_2);
            stream.WriteInt(Player_3);
            stream.WriteInt(Player_4);
        }
    }
}