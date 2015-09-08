namespace Poke.Server.Packets
{
    public enum PacketsClient // -- From Client
    {
        Handshake = 0x00,
        LoginStart = 0x00,
        EncryptionResponse = 0x01,

        Request = 0x00,
        Ping = 0x01,

        KeepAlive = 0x00,
        ChatMessage = 0x01,


        BattleCreate = 0x08,
        BattleAttack, 
        BattleUseItem,
        BattleSwitchPokemon,
        BattleFlee,

        Disconnect = 0x40
    }

    public enum PacketsServer // -- From Server
    {
        LoginDisconnect = 0x00,
        EncryptionRequest = 0x01,
        LoginSuccess = 0x02,
        SetCompressionLogin = 0x03,

        Response = 0x00,
        Ping = 0x01,

        KeepAlive = 0x00,
        JoinGame = 0x01,
        ChatMessage = 0x02,
        TimeUpdate = 0x03,
    }
}
