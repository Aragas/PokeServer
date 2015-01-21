using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using PokeServer.Interfaces;
using PokeServer.IO;
using PokeServer.Packets;
using PokeServer.Packets.Client.Joined;
using PokeServer.Packets.Joined;
using PokeServer.Packets.Joining;

namespace PokeServer
{
    static class Program
    {
        static void Main(string[] args)
        {
            PokeServer battleServer = new PokeServer();
            battleServer.Start(new IPEndPoint(IPAddress.Any, 20500));

            while (true) { Thread.Sleep(50); }

            #region Bulbasaur

            Pokemon bulbasaur = new Pokemon
            {
                PokedexID = 001,

                Level = 5,
                ExperiencePoints = 0,

                Types = new[] {PokemonType.Grass, PokemonType.Poison},
                Abilities = new[] {Ability.Overgrow, Ability.Chlorophyll},

                Move_1 = new Attack
                {
                    Move = Move.Tackle,

                    Type = PokemonType.Normal,
                    Power = 50,
                    Accuracy = 100,

                    Left = 35,
                    TotalAmounth = 35
                },

                Move_2 = new Attack
                {
                    Move = Move.Growl,

                    Type = PokemonType.Normal,
                    Power = 0,
                    Accuracy = 100,

                    Left = 40,
                    TotalAmounth = 40
                },

                Move_3 = null,
                Move_4 = null,


                EggTypes = new[] {EggType.Monster, EggType.Grass}
            };

            #endregion Bulbasaur


            #region Charmander

            Pokemon charmander = new Pokemon
            {
                PokedexID = 004,

                Level = 5,
                ExperiencePoints = 0,

                Types = new[] {PokemonType.Fire},
                Abilities = new[] {Ability.Blaze, Ability.SolarPower},

                Move_1 = new Attack
                {
                    Move = Move.Scratch,

                    Type = PokemonType.Normal,
                    Power = 40,
                    Accuracy = 100,

                    Left = 35,
                    TotalAmounth = 35
                },

                Move_2 = new Attack
                {
                    Move = Move.Growl,

                    Type = PokemonType.Normal,
                    Power = 0,
                    Accuracy = 100,

                    Left = 40,
                    TotalAmounth = 40

                },

                Move_3 = null,
                Move_4 = null,
                EggTypes = new[] {EggType.Monster, EggType.Dragon}
            };

            #endregion Charmander

        }
    }

    public enum NetworkMode { Handshake, ShittyInfo, JoiningServer, JoinedServer }

    public class PokeServer
    {
        private const int ProtocolVersion = 1;

        private int GlobalClientID { get; set; }
        private int GlobalBattleID { get; set; }


        private List<RemoteClient> ClientsNonAuthorized { get; set; }
        private Dictionary<int, RemoteClient> ClientsAuthorized; // -- Using ref

        private Dictionary<string, Player> DataBasePlayers { get; set; } // -- Only load and save. No editions. Do it in Clients
        private Dictionary<int, Player> DataBaseBots { get; set; }    // -- Only load and save. No editions. Do it in Clients

        private Dictionary<int, Battle> CurrentBattles { get; set; }


        private TcpListener Listener { get; set; }

        private object NetworkLock { get; set; }

        private Thread NetworkThread { get; set; }    // -- Send|Receive Packets
        private Thread BattleThread { get; set; }     // -- Battle handling
        private Thread WalkThread { get; set; }       // -- Player walking handling
        private Thread LogicThread { get; set; }      // -- Action with World and NPC 


        public PokeServer()
        {
            GlobalClientID = 0;
            GlobalBattleID = 0;

            // Load Players & Bots
            DataBasePlayers = new Dictionary<string, Player>();
            DataBaseBots = new Dictionary<int, Player>();
            
            CurrentBattles = new Dictionary<int, Battle>();

            NetworkLock = new object();
            ClientsNonAuthorized = new List<RemoteClient>();
            ClientsAuthorized = new Dictionary<int, RemoteClient>();
        }

        public void Start(IPEndPoint endPoint)
        {
            Listener = new TcpListener(endPoint);
            Listener.Start();
            Listener.BeginAcceptTcpClient(AcceptClientAsync, null);

            NetworkThread = new Thread(NetworkWorker);
            BattleThread = new Thread(BattleWorker);
            NetworkThread.Start();
            BattleThread.Start();
        }

        
        #region Network
        
        private void AcceptClientAsync(IAsyncResult result)
        {
            lock (NetworkLock)
            {
                if (Listener == null)
                    return; // Server shutting down

                var client = new RemoteClient(Listener.EndAcceptTcpClient(result));
                ClientsNonAuthorized.Add(client);
                Listener.BeginAcceptTcpClient(AcceptClientAsync, null);
            }
        }

        private void NetworkWorker()
        {
            while (true)
            {
                //UpdateScheduledEvents();

                lock (NetworkLock)
                {

                    #region Non authorized clients

                    var counterUnAuth = 0;

                    for (int i = 0; i < ClientsNonAuthorized.Count; i++)
                    {
                        var client = ClientsNonAuthorized[i];

                        #region Forsed disconnect detection

                        if (counterUnAuth >= 100) // -- Connection check every 1 second
                        {
                            var state = client.NetworkClient.GetState();

                            switch (state)
                            {
                                case TcpState.Closed:
                                    goto Disconnect;

                                case TcpState.Unknown:
                                    if (!client.UnknownStateConnection)
                                    {
                                        client.UnknownStateConnection = true;
                                        client.LastConnected = DateTime.Now;
                                    }

                                    if (client.LastConnected.AddSeconds(10) < DateTime.Now)
                                        goto Disconnect;
                                    break;

                                default:
                                    client.UnknownStateConnection = false;
                                    break;
                            }

                            counterUnAuth = 0;
                        }
                        counterUnAuth++;

                        #endregion Forsed disconnect detection

                        #region Packet sending

                        // Send packets
                        while (client.PacketQueue.Count != 0)
                        {
                            IPacket nextPacket;
                            if (client.PacketQueue.TryDequeue(out nextPacket))
                            {
                                // -- Safely send packets. We can use Forsed disconnect detection to awoid try catch
                                try { client.NetworkManager.SendPacket(nextPacket); }
                                catch (IOException) { goto Disconnect; }

                                // -- Check for Client Disconnect packets
                                if (nextPacket is DisconnectPacket)
                                    goto Disconnect;

                                //if (nextPacket is DisconnectPacket || (nextPacket is StatusPingPacket && client.NetworkManager.NetworkMode == NetworkMode.Status))
                                //    disconnect = true;
                            }
                        }

                        #endregion Packet send

                        #region Packet reading

                        var timeout = DateTime.Now.AddMilliseconds(10);
                        while (client.NetworkClient.Available != 0 && DateTime.Now < timeout)
                        {
                            try
                            {
                                var packet = client.NetworkManager.ReadPacket();
                                if (packet is DisconnectPacket)
                                    goto Disconnect;

                                HandlePacketUnAuth(client, packet);
                            }
                            catch (SocketException) { goto Disconnect; }
                        }

                        #endregion Packet reading

                        #region Logged Client handle

                        if (client.IsLoggedIn) // Is playing
                        {
                            if (client.LastKeepAliveSent.AddSeconds(10) < DateTime.Now)
                            {
                                Random rand = new Random();
                                client.PacketQueue.Enqueue(new KeepAlivePacket { KeepAlive = rand.Next() });
                                client.LastKeepAliveSent = DateTime.Now;
                                // TODO: Confirm keep alive
                            }

                            if (client.LastKeepAliveReceived.AddSeconds(20) > DateTime.Now)
                                goto Disconnect;
                        }

                        #endregion Logged Client handle

                        continue;

                        Disconnect:
                        {
                            DisconnectPlayerUnAuth(client);
                            i--;
                        }
                    }

                    #endregion Non authorized clients

                    #region Authorized clients

                    if (ClientsAuthorized.Count == 0)
                        goto Sleep;

                    var counterAuth = 0;

                    foreach (var client in ClientsAuthorized.Values)
                    {
                        //var client = ClientsAuthorized[i];
                        var clientID = client.PlayerEntity.SessionID;

                        #region Forsed disconnect detection

                        if (counterAuth >= 100) // -- Connection check every 1 second
                        {
                            var state = client.NetworkClient.GetState();

                            switch (state)
                            {
                                case TcpState.Closed: // -- Connection closed
                                    goto Disconnect;

                                case TcpState.Unknown: // -- Connection broken
                                    if (!client.UnknownStateConnection)
                                    {
                                        client.UnknownStateConnection = true;
                                        client.LastConnected = DateTime.Now;
                                    }

                                    if (client.LastConnected.AddSeconds(10) < DateTime.Now) // -- If we have USC more than 10 seconds, disconnect
                                        goto Disconnect;
                                    break;

                                default:
                                    client.UnknownStateConnection = false;
                                    break;
                            }

                            counterAuth = 0;
                        }
                        counterAuth++;

                        #endregion Forsed disconnect detection

                        #region Packet sending

                        // Send packets
                        while (client.PacketQueue.Count != 0)
                        {
                            IPacket nextPacket;
                            if (client.PacketQueue.TryDequeue(out nextPacket))
                            {
                                // -- Safely send packets. We can use Forsed disconnect detection to awoid try catch
                                try { client.NetworkManager.SendPacket(nextPacket); }
                                catch (IOException) { goto Disconnect; }

                                // -- Check for Client Disconnect packets
                                if (nextPacket is DisconnectPacket)
                                    goto Disconnect;

                                //if (nextPacket is DisconnectPacket || (nextPacket is StatusPingPacket && client.NetworkManager.NetworkMode == NetworkMode.Status))
                                //    disconnect = true;
                            }
                        }

                        #endregion Packet send

                        #region Packet reading

                        var timeout = DateTime.Now.AddMilliseconds(10);
                        while (client.NetworkClient.Available != 0 && DateTime.Now < timeout)
                        {
                            try
                            {
                                var packet = client.NetworkManager.ReadPacket();
                                if (packet is DisconnectPacket)
                                    goto Disconnect;

                                HandlePacketAuth(clientID, packet);
                            }
                            catch (SocketException) { goto Disconnect; }
                        }

                        #endregion Packet reading

                        #region Logged Client handle

                        if (client.IsLoggedIn) // Is playing
                        {
                            if (client.LastKeepAliveSent.AddSeconds(10) < DateTime.Now)
                            {
                                Random rand = new Random();
                                client.PacketQueue.Enqueue(new KeepAlivePacket { KeepAlive = rand.Next() });
                                client.LastKeepAliveSent = DateTime.Now;
                                // TODO: Confirm keep alive
                            }

                            if (client.LastKeepAliveReceived.AddSeconds(20) > DateTime.Now)
                                goto Disconnect;
                        }

                        #endregion Logged Client handle

                        continue;

                    Disconnect:
                        {
                            DisconnectPlayerAuth(clientID);
                            //i--;
                        }
                    }

                    //for (int i = 0; i < ClientsAuthorized.Count; i++) { }

                    #endregion Authorized clients

                }

            Sleep:
                Thread.Sleep(10);
            }
        }


        /// <summary>
        /// Send client Disconnect packet if reason != null, save clients PlayerEntity
        /// </summary>
        /// <param name="client"></param>
        /// <param name="reason"></param>
        private void DisconnectPlayerUnAuth(RemoteClient client, string reason = null)
        {
            if (!ClientsNonAuthorized.Contains(client))
                throw new InvalidOperationException("The server is not aware of this client");

            if (client.NetworkClient == null || !client.NetworkClient.Connected)
                return;

            lock (NetworkLock)
            {
                if (reason != null)
                {
                    if (client.NetworkManager.NetworkMode == NetworkMode.JoiningServer)
                        client.PacketQueue.Enqueue(new LoginDisconnectPacket {Reason = @"\" + reason + @"\"});
                    else
                        client.PacketQueue.Enqueue(new DisconnectPacket {Reason = @"\" + reason + @"\"});
                }

                client.NetworkClient.Close();
 

                if (client.IsLoggedIn)
                {
                    SavePlayer(client, client.PlayerEntity.Username);
                    //EntityManager.Despawn(client.Entity);

                    SendChat(string.Format("{0} left the game.", client.Username));
                }
                ClientsNonAuthorized.Remove(client);

                client.Dispose();
            }
        }

        private void HandlePacketUnAuth(RemoteClient client, IPacket packet)
        {
            switch (client.NetworkManager.NetworkMode)
            {
                case NetworkMode.Handshake:
                    var handshake = (HandshakePacket) packet;

                    if (handshake.ProtocolVersion != ProtocolVersion)
                        DisconnectPlayerUnAuth(client, string.Format("Wrong protocol. Server protocol is {0}", ProtocolVersion));
                    break;

                case NetworkMode.JoiningServer:
                    switch (packet.ID)
                    {
                        case 0x05:
                            ClientsNonAuthorized.Remove(client); // -- Delete player from non authorized player list

                            var testPacket = (TestPacket) packet; // -- Load Packet
                            client.PlayerEntity = DataBasePlayers[testPacket.Username];// -- Load Player from database

                            GlobalClientID++; // -- Unique Player ID
                            client.PlayerEntity.SessionID = GlobalClientID;
                            ClientsAuthorized.Add(GlobalClientID, client);

                            client.NetworkManager.NetworkMode = NetworkMode.JoinedServer;
                            break;
                    }

                    //switch ((PacketsClient) packet.ID)
                    //{
                    //}
                    break;

                case NetworkMode.JoinedServer:
                    // -- Not possible
                    break;

                case NetworkMode.ShittyInfo:
                    break;
            }
        }


        private void DisconnectPlayerAuth(int clientID, string reason = null)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new InvalidOperationException("The server is not aware of this client");

            if (ClientsAuthorized[clientID].NetworkClient == null || !ClientsAuthorized[clientID].NetworkClient.Connected)
                return;

            lock (NetworkLock)
            {
                if (reason != null)
                {  
                    if (ClientsAuthorized[clientID].NetworkManager.NetworkMode == NetworkMode.JoiningServer)
                        ClientsAuthorized[clientID].PacketQueue.Enqueue(new LoginDisconnectPacket { Reason = @"\" + reason + @"\" });
                    else
                        ClientsAuthorized[clientID].PacketQueue.Enqueue(new DisconnectPacket { Reason = @"\" + reason + @"\" }); 
                }

                ClientsAuthorized[clientID].NetworkClient.Close();


                if (ClientsAuthorized[clientID].IsLoggedIn)
                {
                    SavePlayer(ClientsAuthorized[clientID], ClientsAuthorized[clientID].PlayerEntity.Username);
                    //EntityManager.Despawn(client.Entity);

                    SendChat(string.Format("{0} left the game.", ClientsAuthorized[clientID].Username));
                }
                ClientsAuthorized[clientID].Dispose();

                ClientsAuthorized.Remove(clientID);
            }
        }

        private void HandlePacketAuth(int clientID, IPacket packet)
        {
            switch (ClientsAuthorized[clientID].NetworkManager.NetworkMode)
            {
                case NetworkMode.Handshake:
                    // -- Not possible
                    break;

                case NetworkMode.JoiningServer:
                    // -- Not possible
                    break;

                case NetworkMode.JoinedServer:
                    switch ((PacketsClient)packet.ID)
                    {
                        case PacketsClient.BattleCreate:
                            var battleCreatePacket = (BattleCreatePacket)packet;
                            BattleCreate(clientID, battleCreatePacket);
                            break;

                        case PacketsClient.BattleAttack:
                            var battleAttackPacket = (BattleAttackPacket)packet;
                            BattleAttack(clientID, battleAttackPacket);
                            break;

                        case PacketsClient.BattleUseItem:
                            var battleUseItemPacket = (BattleUseItemPacket)packet;
                            BattleUseItem(clientID, battleUseItemPacket);
                            break;

                        case PacketsClient.BattleSwitchPokemon:
                            var battleSwitchPokemonPacket = (BattleSwitchPokemonPacket)packet;
                            BattleSwitchPokemon(clientID, battleSwitchPokemonPacket);
                            break;

                        case PacketsClient.BattleFlee:
                            var battleFleePacket = (BattleFleePacket)packet;
                            BattleFlee(clientID, battleFleePacket);
                            break;
                    }
                    break;

                case NetworkMode.ShittyInfo:
                    // -- Not possible
                    break;
            }
        }


        private void SendChat(string message)
        {
            foreach (var client in ClientsAuthorized)
            {
                if (client.Value.IsLoggedIn)
                    client.Value.PacketQueue.Enqueue(new ChatMessagePacket { Message = message });
            }
        }

        #endregion Network


        private void SavePlayer(RemoteClient client, string username)
        {
            DataBasePlayers[username] = client.PlayerEntity;
        }

        private void DoClientUpdates(RemoteClient client)
        {
            //// Update keep alive, chunks, etc
            //if (client.LastKeepAliveSent.AddSeconds(20) < DateTime.Now)
            //{
            //    Random rand = new Random();
            //    client.PacketQueue.Enqueue(new KeepAlivePacket { KeepAlive = rand.Next() });
            //    client.LastKeepAliveSent = DateTime.Now;
            //    // TODO: Confirm keep alive
            //}

            //if (client.BlockBreakStartTime != null)
            //{
            //    byte progress = (byte)((DateTime.Now - client.BlockBreakStartTime.Value).TotalMilliseconds / client.BlockBreakStageTime);
            //    var knownClients = EntityManager.GetKnownClients(client.Entity);
            //    foreach (var c in knownClients)
            //    {
            //        c.SendPacket(new BlockBreakAnimationPacket(client.Entity.EntityId,
            //            client.ExpectedBlockToMine.X, client.ExpectedBlockToMine.Y, client.ExpectedBlockToMine.Z, progress));
            //    }
            //}

            //if (NextChunkUpdate < DateTime.Now) // Once per second
            //{
            //    // Update chunks
            //    if (client.Settings.ViewDistance < client.Settings.MaxViewDistance)
            //    {
            //        client.Settings.ViewDistance++;
            //        client.ForceUpdateChunksAsync();
            //    }
            //}
        }

        private int GetPlayerID(string username)
        {
            if (username == "Aragas")
            {
                return 1;
            }
            return 0;
        }
        

        #region Battle creation

        // -- Player request a battle
        protected internal void BattleCreate(int clientID, BattleCreatePacket packet)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            #region Battle ID Players

            int battleID = 0;
            switch (packet.BattleMode)
            {
                case BattleMode.PlayerVsNature:
                    return;

                case BattleMode.PlayerVsNatureDual:
                    return;

                case BattleMode.PlayerVsBot:
                {
                    var opponents = (PlayersPlayerVsBot) packet.Players;

                    battleID = BattleInit(opponents.Player_1, opponents.Bot_1, opponents.Mode);

                    goto BotCreated;
                }

                case BattleMode.PlayerVsBotDual:
                {
                    var opponents = (PlayersPlayerVsBotDual) packet.Players;

                    battleID = BattleInit(opponents.Player_1, opponents.Bot_1, opponents.Mode);

                    goto BotCreated;
                }

                case BattleMode.PlayerVsPlayer:
                {
                    var opponents = (PlayersPlayerVsPlayer) packet.Players;

                    battleID = BattleInit(opponents.Player_1, opponents.Player_2, opponents.Mode);

                    goto PlayerCreated;
                }

                case BattleMode.PlayerVsPlayerDual:
                {
                    var opponents = (PlayersPlayerVsPlayerDual) packet.Players;

                    battleID = BattleInit(opponents.Player_1, opponents.Player_2, opponents.Mode);

                    goto PlayerCreated;
                }


                case BattleMode.TwoPlayersVsTwoPlayers:
                {
                    var opponents = (PlayersTwoPlayersVsTwoPlayers) packet.Players;

                    battleID = BattleInit(opponents.Player_1, opponents.Player_2, opponents.Player_3, opponents.Player_4, opponents.Mode);
                    
                    goto PlayerCreated;
                }
            }

            #endregion Battle ID Players

            return;

            #region GOTO Bot handling

            BotCreated:
            {
                if (battleID == -1)
                    return;

                CurrentBattles[battleID].CalcFirstTurn();

                BattleSendEnteringBattleToPlayer(CurrentBattles[battleID], OpponentBattle.Opponent_1); // -- Send Player that he entered a battle
                BattleSendStatusPacketToPlayers(battleID); // -- Send all battle info packet
                //stream.SendPacket(new BattleYourTurnPacket()); // -- Send yourturn packet
            }

            #endregion Bot handling

            #region GOTO Player handling

            PlayerCreated:
            {
                if (battleID == -1)
                {
                    ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleCreateStatusPacket
                    {
                        BattleID = battleID,
                        Status = BattleCreateStatus.Error
                    }); // -- Send creation error
                    return;
                }

                CurrentBattles[battleID].CalcFirstTurn();

                ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleCreateStatusPacket
                {
                    BattleID = battleID,
                    Status = BattleCreateStatus.Success
                }); // -- Send creation success

                BattleSendEnteringBattlePacketToPlayers(battleID); // -- Send players that they entered a battle
                BattleSendStatusPacketToPlayers(battleID); // -- Send all battle info packet
                //stream.SendPacket(new BattleYourTurnPacket()); // -- Send yourturn packet
            }

            #endregion Player handling
        }

        // Bot | Nature request a battle with player
        protected internal void BattleCreate(int clientID, int botID, BattleMode mode)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            if (!DataBaseBots.ContainsKey(botID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            #region Battle ID Players

            int battleID = 0;
            switch (mode)
            {
                case BattleMode.PlayerVsNature:
                    return;

                case BattleMode.PlayerVsNatureDual:
                    return;

                case BattleMode.PlayerVsBot:
                    battleID = BattleInit(clientID, botID, mode);

                    goto BotCreated;
                    

                case BattleMode.PlayerVsBotDual:
                    battleID = BattleInit(clientID, botID, mode);

                    goto BotCreated;

                case BattleMode.PlayerVsPlayer:
                    return;

                case BattleMode.PlayerVsPlayerDual:
                    return;


                case BattleMode.TwoPlayersVsTwoPlayers:
                    return;
            }

            #endregion Battle ID Players

            return;

            #region GOTO Bot handling

            BotCreated:
            {
                if (battleID == -1)
                    return;

                CurrentBattles[battleID].CalcFirstTurn();

                BattleSendEnteringBattleToPlayer(CurrentBattles[battleID], OpponentBattle.Opponent_1); // -- Send Player that he entered a battle
                BattleSendStatusPacketToPlayers(battleID); // -- Send all battle info packet
                //stream.SendPacket(new BattleYourTurnPacket()); // -- Send yourturn packet
            }

            #endregion Bot handling
        }

        private int BattleInit(int player_1, int player_2, BattleMode mode)
        {
            // -- Check if Player exist.
            if (!ClientsAuthorized.ContainsKey(player_1) || !ClientsAuthorized.ContainsKey(player_2))
                return -1;

            // -- Check if Player is in battle already.
            if (ClientsAuthorized[player_1].PlayerEntity.InBattle || ClientsAuthorized[player_2].PlayerEntity.InBattle)
                return -1;

            // -- Check if players are nearby
            // -- Check if players are nearby

            GlobalBattleID++;

            ClientsAuthorized[player_1].PlayerEntity.InBattle = true;
            ClientsAuthorized[player_2].PlayerEntity.InBattle = true;

            // -- Create battle.
            Battle battle = new Battle(GlobalBattleID, ClientsAuthorized[player_1], ClientsAuthorized[player_2], mode);

            CurrentBattles.Add(battle.BattleID, battle);
            return battle.BattleID;


            //// -- Check if Player exist.
            //if (!Players.ContainsKey(player_1) || !Players.ContainsKey(player_2))
            //    return -1;

            //// -- Check if Player is in battle already.
            //if (Players[player_1].InBattle || Players[player_2].InBattle)
            //    return -1;

            //// -- Check if players are nearby
            //// -- Check if players are nearby

            //_battleID++;

            //Players[player_1].InBattle = true;
            //Players[player_2].InBattle = true;

            //// -- Create battle.
            //Battle battle = new Battle(_battleID, new Opponent(Players[player_1]), new Opponent(Players[player_2]), mode);

            //CurrentBattles.Add(battle.BattleID, battle);
            //return battle.BattleID;
        }
        private int BattleInit(int player_1, int player_2, int player_3, int player_4, BattleMode mode)
        {
            // -- Check if Player exist.
            if (!ClientsAuthorized.ContainsKey(player_1) || !ClientsAuthorized.ContainsKey(player_2) || !ClientsAuthorized.ContainsKey(player_3) || !ClientsAuthorized.ContainsKey(player_4))
                return -1;

            // -- Check if Player is in battle already.
            if (ClientsAuthorized[player_1].PlayerEntity.InBattle || ClientsAuthorized[player_2].PlayerEntity.InBattle || ClientsAuthorized[player_3].PlayerEntity.InBattle || ClientsAuthorized[player_4].PlayerEntity.InBattle)
                return -1;

            // -- Check if players are nearby
            // -- Check if players are nearby

            GlobalBattleID++;

            ClientsAuthorized[player_1].PlayerEntity.InBattle = true;
            ClientsAuthorized[player_2].PlayerEntity.InBattle = true;
            ClientsAuthorized[player_3].PlayerEntity.InBattle = true;
            ClientsAuthorized[player_4].PlayerEntity.InBattle = true;

            // -- Create battle.
            Battle battle = new Battle(GlobalBattleID, ClientsAuthorized[player_1], ClientsAuthorized[player_2], ClientsAuthorized[player_3], ClientsAuthorized[player_4], mode);

            CurrentBattles.Add(battle.BattleID, battle);
            return battle.BattleID;


            //// -- Check if Player exist.
            //if (!Players.ContainsKey(player_1) || !Players.ContainsKey(player_2) || !Players.ContainsKey(player_3) || !Players.ContainsKey(player_4))
            //    return -1;

            //// -- Check if Player is in battle already.
            //if (!Players[player_1].InBattle || Players[player_2].InBattle || Players[player_3].InBattle || Players[player_4].InBattle)
            //    return -1;

            //// -- Check if players are nearby
            //// -- Check if players are nearby

            //_battleID++;

            //Players[player_1].InBattle = true;
            //Players[player_2].InBattle = true;
            //Players[player_3].InBattle = true;
            //Players[player_4].InBattle = true;

            //// -- Create battle.
            //Battle battle = new Battle(_battleID, new Opponent(Players[player_1]), new Opponent(Players[player_2]), new Opponent(Players[player_3]), new Opponent(Players[player_4]), mode);

            //CurrentBattles.Add(battle.BattleID, battle);
            //return battle.BattleID;
        }

        #endregion Battle creation

        #region Battle basic

        protected internal void BattleAttack(int clientID, BattleAttackPacket packet)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            if (!CurrentBattles.ContainsKey(packet.BattleID))
                ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleNotFoundPacket { BattleID = packet.BattleID }); // -- Battle not found

            CurrentBattles[packet.BattleID].Attack(packet.OpponentAttacker, packet.PokemonAttacker, packet.Move, packet.OpponentDefender, packet.PokemonDefender); // -- Do it
            
            BattleSendStatusPacketToPlayers(packet.BattleID); // -- Send all battle info packet
        }

        protected internal void BattleUseItem(int clientID, BattleUseItemPacket packet)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            if (!CurrentBattles.ContainsKey(packet.BattleID))
                ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleNotFoundPacket { BattleID = packet.BattleID }); // -- Battle not found

            CurrentBattles[packet.BattleID].UseItem(packet.ItemUser, packet.Pokemon, packet.Item); // -- Do it
            
            BattleSendStatusPacketToPlayers(packet.BattleID); // -- Send all battle info packet
        }

        protected internal void BattleSwitchPokemon(int clientID, BattleSwitchPokemonPacket packet)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            if (!CurrentBattles.ContainsKey(packet.BattleID))
                ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleNotFoundPacket { BattleID = packet.BattleID }); // -- Battle not found

            CurrentBattles[packet.BattleID].SwitchPokemon(packet.Switcher, packet.Pokemon, packet.SwitchPokemon); // -- Do it
            
            BattleSendStatusPacketToPlayers(packet.BattleID); // -- Send all battle info packet
        }

        protected internal void BattleFlee(int clientID, BattleFleePacket packet)
        {
            if (!ClientsAuthorized.ContainsKey(clientID))
                throw new Exception("PokeServer: Battle creation - Client ID not found");

            if (!CurrentBattles.ContainsKey(packet.BattleID))
                ClientsAuthorized[clientID].PacketQueue.Enqueue(new BattleNotFoundPacket { BattleID = packet.BattleID }); // -- Battle not found

            CurrentBattles[packet.BattleID].Flee(packet.User, packet.Pokemon); // -- Do it
            
            BattleSendStatusPacketToPlayers(packet.BattleID); // -- Send all battle info packet
        }

        #endregion Battle basic
        
        #region Battle send EnteringBattlePacket

        private void BattleSendEnteringBattlePacketToPlayers(int battleID)
        {
            Battle battle = CurrentBattles[battleID];

            switch (battle.Mode)
            {
                case BattleMode.PlayerVsNature:
                case BattleMode.PlayerVsNatureDual:
                case BattleMode.PlayerVsBot:
                case BattleMode.PlayerVsBotDual:
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2
                    BattleSendEnteringBattleToBot(battle, OpponentBattle.Opponent_2);     // -- Filter Opponent_1
                    break;

                case BattleMode.PlayerVsPlayer:
                case BattleMode.PlayerVsPlayerDual:
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_2);  // -- Filter Opponent_1
                    break;

                case BattleMode.TwoPlayersVsTwoPlayers:
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2, Opponent_3, Opponent_4
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_2);  // -- Filter Opponent_1, Opponent_3, Opponent_4
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_3);  // -- Filter Opponent_1, Opponent_2, Opponent_4
                    BattleSendEnteringBattleToPlayer(battle, OpponentBattle.Opponent_4);  // -- Filter Opponent_1, Opponent_2, Opponent_3
                    break;
            }
        }
        private void BattleSendEnteringBattleToPlayer(Battle battle, OpponentBattle opponent)
        {
            RemoteClient client = battle.GetRemoteClient(opponent); // -- Get Player
            client.PacketQueue.Enqueue(new BattleEnteringBattlePacket { BattleID = battle.BattleID });
            client.PacketQueue.Enqueue(new BattleStatusPacket { BattleID = battle.BattleID, Battle = battle.FilterBattle(opponent), Message = "" });
        }
        private void BattleSendEnteringBattleToBot(Battle battle, OpponentBattle opponent)
        {
            int botID = battle.GetBotID(opponent); // -- Get Bot ID
            //stream.SendPacket(new BattleEnteringBattlePacket { BattleID = battle.BattleID });
            //stream.SendPacket(new BattleStatusPacket { BattleID = battle.BattleID, Battle = battle.FilterBattle(opponent), Message = "" });
        }

        #endregion Battle send EnteringBattlePacket

        #region Battle send StatusPacket

        private void BattleSendStatusPacketToPlayers(int battleID)
        {
            Battle battle = CurrentBattles[battleID];

            switch (battle.Mode)
            {
                case BattleMode.PlayerVsNature:
                case BattleMode.PlayerVsNatureDual:
                case BattleMode.PlayerVsBot:
                case BattleMode.PlayerVsBotDual:
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2
                    BattleSendStatusToBot(battle, OpponentBattle.Opponent_2);     // -- Filter Opponent_1
                    break;

                case BattleMode.PlayerVsPlayer:
                case BattleMode.PlayerVsPlayerDual:
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_2);  // -- Filter Opponent_1
                    break;

                case BattleMode.TwoPlayersVsTwoPlayers:
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_1);  // -- Filter Opponent_2, Opponent_3, Opponent_4
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_2);  // -- Filter Opponent_1, Opponent_3, Opponent_4
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_3);  // -- Filter Opponent_1, Opponent_2, Opponent_4
                    BattleSendStatusToPlayer(battle, OpponentBattle.Opponent_4);  // -- Filter Opponent_1, Opponent_2, Opponent_3
                    break;
            }
        }
        private void BattleSendStatusToPlayer(Battle battle, OpponentBattle opponent)
        {
            RemoteClient client = battle.GetRemoteClient(opponent); // -- Get Player
            client.PacketQueue.Enqueue(new BattleStatusPacket { BattleID = battle.BattleID, Battle = battle.FilterBattle(opponent), Message = "" });
        }
        private void BattleSendStatusToBot(Battle battle, OpponentBattle opponent)
        {
            int botID = battle.GetBotID(opponent); // -- Get Bot ID
            //stream.SendPacket(new BattleStatusPacket { BattleID = battle.BattleID, Battle = battle.FilterBattle(opponent), Message = "" });
        }

        #endregion Battle send StatusPacket

        private void BattleEnd(int battleID)
        {
            if (!CurrentBattles.ContainsKey(battleID))
                throw new Exception("PokeServer: Battle ending - Battle ID not found");

            CurrentBattles[battleID].EndBattle();
            CurrentBattles[battleID].SavePlayers(ref ClientsAuthorized);

            CurrentBattles.Remove(battleID);

            //stream.SendPacket(new BattleEndPacket { BattleID = battleID });
        }


        private void BattleWorker(object state)
        {
            for (int i = 0; i < CurrentBattles.Count; i++)
            {
                if (CurrentBattles[i].DoTick())         // -- Makes a tick in our timer. If (1) second passed, handle battle
                {
                    if (!CurrentBattles[i].TurnEnded)   // -- If we don't received an command, do nothing
                        continue;
                                                        // -- Else handle battle
                                                        
                    CurrentBattles[i].CalcNextTurn();
                    // Send all battle info packet
                    // Send yourturn packet
                }
            }

            // -- Sleep for one second.
            Thread.Sleep(50);
        }
       
        
        public int FindBattle(int playerID)
        {
            if (CurrentBattles.Count == 0)
                return -1;

            foreach (var battle in CurrentBattles.Values)
            {
                RemoteClient client = null;

                client = battle.GetRemoteClient(OpponentBattle.Opponent_1);
                if (client != null)
                {
                    if (client.PlayerEntity.SessionID == playerID)
                        return battle.BattleID;
                }

                client = battle.GetRemoteClient(OpponentBattle.Opponent_2);
                if (battle.GetRemoteClient(OpponentBattle.Opponent_2) != null)
                {
                    if (client.PlayerEntity.SessionID == playerID)
                        return battle.BattleID;
                }

                client = battle.GetRemoteClient(OpponentBattle.Opponent_3);
                if (battle.GetRemoteClient(OpponentBattle.Opponent_3) != null)
                {
                    if (client.PlayerEntity.SessionID == playerID)
                        return battle.BattleID;
                }

                client = battle.GetRemoteClient(OpponentBattle.Opponent_4);
                if (battle.GetRemoteClient(OpponentBattle.Opponent_4) != null)
                {
                    if (client.PlayerEntity.SessionID == playerID)
                        return battle.BattleID;
                }
            }
            return -1;
        }
    }


    public interface IClient { bool IsBot { get; } Player PlayerEntity { get; set; } }

    /// <summary>
    /// Real Player
    /// </summary>
    public class RemoteClient : IClient
    {
        public TcpClient NetworkClient { get; private set; }
        public Stream NetworkStream { get; private set; }
        public PlayerStream NetworkManager { get; private set; }

        public bool IsLoggedIn { get; set; }

        public ConcurrentQueue<IPacket> PacketQueue { get; set; }

        //public ClientSettings Settings { get; set; }

        public string Username { get; set; }

        public bool IsBot { get { return false; } }

        public Player PlayerEntity { get; set; }

        protected internal bool UnknownStateConnection { get; set; }
        protected internal DateTime LastConnected { get; set; }

        protected internal DateTime LastKeepAliveReceived { get; set; }
        protected internal DateTime LastKeepAliveSent { get; set; }

        protected internal byte[] VerificationToken { get; set; }

        public RemoteClient(TcpClient client)
        {
            NetworkClient = client;
            NetworkStream = NetworkClient.GetStream();
            NetworkManager = new PlayerStream(NetworkStream);
            PacketQueue = new ConcurrentQueue<IPacket>();
        }


        public void Dispose()
        {
            //if (PlayerManager != null)
            //    PlayerManager.Dispose();
        }
    }

    /// <summary>
    /// Bot
    /// </summary>
    public class PokeBot : IClient { public bool IsBot { get { return true; } } public Player PlayerEntity { get; set; } }


    public enum OpponentBattle
    {
        Opponent_1,
        Opponent_2,
        Opponent_3,
        Opponent_4,
        All,
        AllAndAttacker
    }

    public enum PokemonBattle
    {
        Pokemon_1,
        Pokemon_2,
        All
    }

    public enum MoveBattle
    {
        Move_1,
        Move_2,
        Move_3,
        Move_4
    }

    public enum PokemonToSwitch
    {
        Pokemon_1,
        Pokemon_2,
        Pokemon_3,
        Pokemon_4,
        Pokemon_5,
        Pokemon_6
    }

    public enum BattleMode
    {
        PlayerVsNature,
        PlayerVsNatureDual,

        PlayerVsBot,
        PlayerVsBotDual,

        PlayerVsPlayer,
        PlayerVsPlayerDual,

        TwoPlayersVsTwoPlayers
    }

    public enum BattleStatus
    {
        Correct,
        NotPossible,
        Corrected
    }


    public class Battle
    {
        public int BattleID { get; private set; }
        public BattleMode Mode { get; private set; }

        public bool TurnEnded { get; private set; }

        private byte Time = 0;

        public OpponentBattle CurrentTurnOpponent;  // -- Opponent that have currentrly turn
        public PokemonBattle CurrentTurnPokemon;    // -- Opponents pokemon  that have currently turn

        public Opponent Opponent_1 { get; private set; }
        public Opponent Opponent_2 { get; private set; }
        public Opponent Opponent_3 { get; private set; }
        public Opponent Opponent_4 { get; private set; }


        public Battle(int battleID, IClient client_1, IClient client_2, BattleMode mode)
        {
            BattleID = battleID;
            Mode = mode;

            if (Mode == BattleMode.TwoPlayersVsTwoPlayers)
                throw new Exception("Battle Creation: Incorrect BattleMode (Not 1v1)");

            Opponent_1 = new Opponent(client_1, mode);
            Opponent_2 = new Opponent(client_2, mode);
            Opponent_3 = null;
            Opponent_4 = null;
        }

        public Battle(int battleID, IClient client_1, IClient client_2, IClient client_3, IClient client_4, BattleMode mode)
        {
            BattleID = battleID;
            Mode = mode;

            if (Mode != BattleMode.TwoPlayersVsTwoPlayers)
                throw new Exception("Battle Creation: Incorrect BattleMode (Not 2v2)");

            Opponent_1 = new Opponent(client_1, mode); 
            Opponent_2 = new Opponent(client_2, mode);
            Opponent_3 = new Opponent(client_3, mode);
            Opponent_4 = new Opponent(client_4, mode);
        }

        public void CalcFirstTurn() { }

        public void CalcNextTurn() { }

        /// <summary>
        /// If true, switch to next opponent
        /// </summary>
        /// <returns></returns>
        public bool DoTick()
        {
            Time ++;

            if (Time >= 60)
            {
                Time = 0;
                return true;
            }
            else
                return false;
        }

        public void Attack(OpponentBattle oAttacker, PokemonBattle pAttacker, MoveBattle attackMove, OpponentBattle oDefender, PokemonBattle pDefender)
        {
            switch (oAttacker)
            {
                #region Opponent 1

                case OpponentBattle.Opponent_1:
                    switch (oDefender)
                    {
                        case OpponentBattle.Opponent_1:
                            // Self attack
                            Opponent_1.Attack(Opponent_1, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_2:
                            Opponent_1.Attack(Opponent_2, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_3:
                            Opponent_1.Attack(Opponent_3, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_4:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.All:
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.AllAndAttacker:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;
                    }
                    break;

                #endregion

                #region Opponent 2

                case OpponentBattle.Opponent_2:
                    switch (oDefender)
                    {
                        case OpponentBattle.Opponent_1:
                            Opponent_2.Attack(Opponent_1, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_2:
                            // Self attack
                            Opponent_2.Attack(Opponent_2, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_3:
                            Opponent_2.Attack(Opponent_3, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_4:
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.All:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.AllAndAttacker:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;
                    }
                    break;

                #endregion

                #region Opponent 3

                case OpponentBattle.Opponent_3:
                    switch (oDefender)
                    {
                        case OpponentBattle.Opponent_1:
                            Opponent_3.Attack(Opponent_1, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_2:
                            Opponent_3.Attack(Opponent_2, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_3:
                            // Self attack
                            Opponent_3.Attack(Opponent_3, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_4:
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.All:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.AllAndAttacker:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;
                    }
                    break;

                #endregion

                #region Opponent 4

                case OpponentBattle.Opponent_4:
                    switch (oDefender)
                    {
                        case OpponentBattle.Opponent_1:
                            Opponent_4.Attack(Opponent_1, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_2:
                            Opponent_4.Attack(Opponent_2, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_3:
                            Opponent_4.Attack(Opponent_3, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.Opponent_4:
                            // Self attack
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.All:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;

                        case OpponentBattle.AllAndAttacker:
                            Opponent_1.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_2.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_3.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            Opponent_4.Attack(Opponent_4, pAttacker, pDefender, attackMove);
                            break;
                    }
                    break;

                #endregion

                case OpponentBattle.All:
                    throw new Exception("Battle: Attack incorrect enum OpponentBattle.All");
                    break;

                case OpponentBattle.AllAndAttacker:
                    throw new Exception("Battle: Attack incorrect enum OpponentBattle.AllAndAttacker");
                    break;
            }
        }

        public void UseItem(OpponentBattle itemUser, PokemonBattle pokemon, Item item)
        {
            switch (itemUser)
            {
                case OpponentBattle.Opponent_1:
                    Opponent_1.UseItem(pokemon, item);
                    break;

                case OpponentBattle.Opponent_2:
                    Opponent_2.UseItem(pokemon, item);
                    break;

                case OpponentBattle.Opponent_3:
                    Opponent_3.UseItem(pokemon, item);
                    break;

                case OpponentBattle.Opponent_4:
                    Opponent_4.UseItem(pokemon, item);
                    break;

                case OpponentBattle.All:
                    throw new Exception("Battle: UseItem incorrect enum OpponentBattle.All");

                case OpponentBattle.AllAndAttacker:
                    Opponent_1.UseItem(pokemon, item);
                    Opponent_2.UseItem(pokemon, item);
                    Opponent_3.UseItem(pokemon, item);
                    Opponent_4.UseItem(pokemon, item);
                    break;
            }
        }

        public void SwitchPokemon(OpponentBattle attacker, PokemonBattle pokemon, PokemonToSwitch switchPokemon)
        {
            switch (attacker)
            {
                case OpponentBattle.Opponent_1:
                    Opponent_1.SwitchPokemon(pokemon, switchPokemon);
                    break;

                case OpponentBattle.Opponent_2:
                    Opponent_2.SwitchPokemon(pokemon, switchPokemon);
                    break;

                case OpponentBattle.Opponent_3:
                    Opponent_3.SwitchPokemon(pokemon, switchPokemon);
                    break;

                case OpponentBattle.Opponent_4:
                    Opponent_4.SwitchPokemon(pokemon, switchPokemon);
                    break;

                case OpponentBattle.All:
                    throw new Exception("Battle: SwitchPokemon incorrect enum OpponentBattle.All");

                case OpponentBattle.AllAndAttacker:
                    throw new Exception("Battle: SwitchPokemon incorrect enum OpponentBattle.AllAndAttacker");
            }
        }

        public void Flee(OpponentBattle attacker, PokemonBattle pokemon)
        {
            if (Mode != BattleMode.PlayerVsNature && Mode != BattleMode.PlayerVsNatureDual) 
                return;

            Random rand = new Random();
            int number = rand.Next(0, 1);

            if (number == 1)
                ; // Flee.
        }

        public void EndBattle() { }


        private void Evolve(Opponent opponent, Pokemon pokemon) { }


        public static Battle FromReader(IProtocolDataReader reader) { return null; }

        public void ToStream(IProtocolStream stream) { }

        
        /// <summary>
        /// Filter Battle for sending it without other players info
        /// </summary>
        /// <param name="notFilter">Opponent that won't be filtered</param>
        /// <returns></returns>
        public Battle FilterBattle(OpponentBattle notFilter)
        {
            Battle battle = this;

            switch (notFilter)
            {
                #region Opponent 1

                case OpponentBattle.Opponent_1:
                    if (Opponent_2 != null)
                        battle.Opponent_2.FilterOpponent();

                    if (Opponent_3 != null)
                        battle.Opponent_3.FilterOpponent();

                    if (Opponent_4 != null)
                        battle.Opponent_4.FilterOpponent();
                    break;

                #endregion

                #region Opponent 2

                case OpponentBattle.Opponent_2:
                    if (Opponent_1 != null)
                        battle.Opponent_1.FilterOpponent();

                    if (Opponent_3 != null)
                        battle.Opponent_3.FilterOpponent();

                    if (Opponent_4 != null)
                        battle.Opponent_4.FilterOpponent();
                    break;

                #endregion

                #region Opponent 3

                case OpponentBattle.Opponent_3:
                    if (Opponent_1 != null)
                        battle.Opponent_1.FilterOpponent();

                    if (Opponent_2 != null)
                        battle.Opponent_2.FilterOpponent();

                    if (Opponent_4 != null)
                        battle.Opponent_4.FilterOpponent();
                    break;

                #endregion

                #region Opponent 4

                case OpponentBattle.Opponent_4:
                    if (Opponent_1 != null)
                        battle.Opponent_1.FilterOpponent();

                    if (Opponent_2 != null)
                        battle.Opponent_2.FilterOpponent();

                    if (Opponent_3 != null)
                        battle.Opponent_3.FilterOpponent();
                    break;

                #endregion

                case OpponentBattle.All:
                    throw new Exception("Battle: FilterBattle incorrect enum OpponentBattle.All");

                case OpponentBattle.AllAndAttacker:
                    throw new Exception("Battle: FilterBattle incorrect enum OpponentBattle.AllAndAttacker");
            }

            return battle;
        }

        public RemoteClient GetRemoteClient(OpponentBattle player)
        {
            switch (player)
            {
                case OpponentBattle.Opponent_1:
                    if (Opponent_1 != null && !Opponent_1.IsBot)
                        return Opponent_1.GetRemoteClient();
                    break;

                case OpponentBattle.Opponent_2:
                    if (Opponent_2 != null && !Opponent_2.IsBot)
                        return Opponent_2.GetRemoteClient();
                    break;

                case OpponentBattle.Opponent_3:
                    if (Opponent_3 != null && !Opponent_3.IsBot)
                        return Opponent_3.GetRemoteClient();
                    break;

                case OpponentBattle.Opponent_4:
                    if (Opponent_4 != null && !Opponent_4.IsBot)
                        return Opponent_4.GetRemoteClient();
                    break;

                case OpponentBattle.All:
                    throw new Exception("Battle: GetRemoteClient incorrect enum OpponentBattle.All");

                case OpponentBattle.AllAndAttacker:
                    throw new Exception("Battle: GetRemoteClient incorrect enum OpponentBattle.AllAndAttacker");
            }

            return null;
        }

        public int GetBotID(OpponentBattle bot)
        {
            switch (bot)
            {
                case OpponentBattle.Opponent_1:
                    if (Opponent_1 != null && Opponent_1.IsBot)
                        return Opponent_1.GetBotID();
                    break;

                case OpponentBattle.Opponent_2:
                    if (Opponent_2 != null && Opponent_2.IsBot)
                        return Opponent_2.GetBotID();
                    break;

                case OpponentBattle.Opponent_3:
                    if (Opponent_3 != null && Opponent_3.IsBot)
                        return Opponent_3.GetBotID();
                    break;

                case OpponentBattle.Opponent_4:
                    if (Opponent_4 != null && Opponent_4.IsBot)
                        return Opponent_4.GetBotID();
                    break;

                case OpponentBattle.All:
                    throw new Exception("Battle: GetBotID incorrect enum OpponentBattle.All");

                case OpponentBattle.AllAndAttacker:
                    throw new Exception("Battle: GetBotID incorrect enum OpponentBattle.AllAndAttacker");
            }

            return -1;
        }

        /// <summary>
        /// Save IClients and dispose them in Battle class
        /// </summary>
        /// <param name="clients"></param>
        public void SavePlayers(ref Dictionary<int, RemoteClient> clients)
        {
            // -- TODO: Dispose bots

            if (Opponent_1 != null && !Opponent_1.IsBot)
            {
                Opponent_1.Player.InBattle = false;
                clients[Opponent_1.Player.SessionID] = Opponent_1.GetRemoteClient();

                Opponent_1.Dispose();
            }

            if (Opponent_2 != null && !Opponent_2.IsBot)
            {
                Opponent_2.Player.InBattle = false;
                clients[Opponent_2.Player.SessionID] = Opponent_2.GetRemoteClient();

                Opponent_2.Dispose();
            }

            if (Opponent_3 != null && !Opponent_3.IsBot)
            {
                Opponent_3.Player.InBattle = false;
                clients[Opponent_3.Player.SessionID] = Opponent_3.GetRemoteClient();

                Opponent_3.Dispose();
            }

            if (Opponent_4 != null && !Opponent_4.IsBot)
            {
                Opponent_4.Player.InBattle = false;
                clients[Opponent_4.Player.SessionID] = Opponent_4.GetRemoteClient();

                Opponent_4.Dispose();
            }
        }
    }


    public class Opponent : IDisposable
    {
        public IClient Client { get; private set; }

        public bool IsBot { get { return Client.IsBot; } }

        public Player Player { get { return Client.PlayerEntity; } }
        public Pokemon Pokemon_1 { get { return Player.Pokemon_1; } }
        public Pokemon Pokemon_2 { get { return Player.Pokemon_2; } }

        private BattleMode Mode { get; set; }

        //private Pokemon CurrentTurn { get; set; }


        public Opponent(IClient player, BattleMode mode)
        {
            Client = player;
            Mode = mode;
        }


        public void Attack(Opponent opponent, PokemonBattle attacker, PokemonBattle defender, MoveBattle move)
        {
            switch (attacker)
            {
                case PokemonBattle.Pokemon_1:
                    switch (defender)
                    {
                        case PokemonBattle.Pokemon_1:
                            Pokemon_1.Attack(opponent.Pokemon_1, move);
                            break;

                        case PokemonBattle.Pokemon_2:
                            Pokemon_1.Attack(opponent.Pokemon_2, move);
                            break;

                        case PokemonBattle.All:
                            Pokemon_1.Attack(opponent.Pokemon_1, move);
                            Pokemon_1.Attack(opponent.Pokemon_2, move);
                            break;
                    }
                    break;

                case PokemonBattle.Pokemon_2:
                    switch (defender)
                    {
                        case PokemonBattle.Pokemon_1:
                            Pokemon_2.Attack(opponent.Pokemon_1, move);
                            break;

                        case PokemonBattle.Pokemon_2:
                            Pokemon_2.Attack(opponent.Pokemon_2, move);
                            break;

                        case PokemonBattle.All:
                            Pokemon_2.Attack(opponent.Pokemon_1, move);
                            Pokemon_2.Attack(opponent.Pokemon_2, move);
                            break;
                    }
                    break;

                case PokemonBattle.All:
                    // Not possible
                    break;
            }
        }

        public void UseItem(PokemonBattle pokemon, Item item)
        {
            if (pokemon == PokemonBattle.All)
            {
                Pokemon_1.UseItem(item);
                Pokemon_2.UseItem(item);
            }
            else
                Player.UseItem((PokemonToSwitch) pokemon, item);
        }

        public void SwitchPokemon(PokemonBattle oldPokemon, PokemonToSwitch newPokemon)
        {
            if(oldPokemon == PokemonBattle.All)
                throw new Exception("Opponent: SwitchPokemon() PokemonBattle.All not possible");

            Player.SwitchPokemons((PokemonToSwitch) oldPokemon, newPokemon);
        }


        public void FilterOpponent()
        {
            //CurrentTurn = null;
            Player.FilterPlayer();
            Pokemon_1.FilterPokemon();
            Pokemon_2.FilterPokemon();
        }

        public RemoteClient GetRemoteClient()
        {
            return IsBot ? null : (RemoteClient) Client;
        }

        public int GetBotID()
        {
            return -1;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public class Player
    {
        public int SessionID;       // -- Random number given by login
        public string Username;     //  -- Username

        public bool InBattle;

        public Pokemon Pokemon_1;
        public Pokemon Pokemon_2;
        public Pokemon Pokemon_3;
        public Pokemon Pokemon_4;
        public Pokemon Pokemon_5;
        public Pokemon Pokemon_6;


        public void UseItem(PokemonToSwitch pokemon, Item item)
        {
            switch (pokemon)
            {
                case PokemonToSwitch.Pokemon_1:
                    Pokemon_1.UseItem(item);
                    break;

                case PokemonToSwitch.Pokemon_2:
                    Pokemon_2.UseItem(item);
                    break;

                case PokemonToSwitch.Pokemon_3:
                    Pokemon_3.UseItem(item);
                    break;

                case PokemonToSwitch.Pokemon_4:
                    Pokemon_4.UseItem(item);
                    break;

                case PokemonToSwitch.Pokemon_5:
                    Pokemon_5.UseItem(item);
                    break;

                case PokemonToSwitch.Pokemon_6:
                    Pokemon_6.UseItem(item);
                    break;
            }
        }

        public void SwitchPokemons(PokemonToSwitch oldPokemon, PokemonToSwitch newPokemon)
        {
            Pokemon temp;

            switch (oldPokemon)
            {
                #region Pokemon 1

                case PokemonToSwitch.Pokemon_1:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_1;

                            Pokemon_1 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion

                #region Pokemon 2

                case PokemonToSwitch.Pokemon_2:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_2;

                            Pokemon_2 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion

                #region Pokemon 3

                case PokemonToSwitch.Pokemon_3:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_3;

                            Pokemon_3 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion

                #region Pokemon 4

                case PokemonToSwitch.Pokemon_4:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_4;

                            Pokemon_4 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion

                #region Pokemon 5

                case PokemonToSwitch.Pokemon_5:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_5;

                            Pokemon_5 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion

                #region Pokemon 6

                case PokemonToSwitch.Pokemon_6:
                    switch (newPokemon)
                    {
                        case PokemonToSwitch.Pokemon_1:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_1;
                            Pokemon_1 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_2:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_2;
                            Pokemon_2 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_3:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_3;
                            Pokemon_3 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_4:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_4;
                            Pokemon_4 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_5:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_5;
                            Pokemon_5 = temp;
                            break;

                        case PokemonToSwitch.Pokemon_6:
                            temp = Pokemon_6;

                            Pokemon_6 = Pokemon_6;
                            Pokemon_6 = temp;
                            break;
                    }
                    break;

                #endregion
            }
        }

        public void FilterPlayer()
        {
            Pokemon_1 = null;
            Pokemon_2 = null;
            Pokemon_3 = null;
            Pokemon_4 = null;
            Pokemon_5 = null;
            Pokemon_6 = null;
        }

        public static Player FromReader(IProtocolDataReader reader) { return null; }

        public void ToStream(IProtocolStream stream) { }
    }


    public class Pokemon
    {
        public struct ContestStats
        {
            public byte Cool;
            public byte Beauty;
            public byte Cute;
            public byte Smart;
            public byte Tough;
            public byte Sheen;
        }

        public struct MainStatsEffort
        {
            public byte HP;
            public byte Attack;
            public byte Defense;
            public byte Speed;
            public byte SPAttack;
            public byte SPDefense;
        }

        #region Global info

        public ushort PokedexID;
        public string Nickname;

        public uint PID; // -- Personality ID

        public ushort HeldItem;

        public ushort OTID;         // -- Original Trainer ID
        public string OTUsername;   // -- Original Trainer Username

        public byte Level;
        public uint ExperiencePoints;

        public PokemonType[] Types;

        public byte Gender; // --  0 - Male, 1 - Female

        public EggType[] EggTypes;

        public Ability[] Abilities;

        #endregion


        #region Catch info

        public byte Pokeball;

        public ushort MetAtLocation;
        public ushort EggLocation;

        public DateTime DateEggReceived;
        public DateTime DateMet;

        #endregion


        #region Move info

        public Attack Move_1;
        public Attack Move_2;
        public Attack Move_3;
        public Attack Move_4;

        #endregion


        #region Stats info

        public byte Nature;

        public byte Flags; // -- Bit 0 - Fateful Encounter Flag, Bit 1 - Female, Bit 2 - Genderless, Bit 3-7 - Alternate Forms

        public uint Stats; // -- Bits 0-29 - Individual Values, HP (<< 0), Attack (<< 5), Defense (<< 10), Speed (<< 15), SP Attack (<< 20), SP Defense (<< 25), Bit 30 - IsEgg, Bit 31 - IsNicknamed

        public MainStatsEffort StatsEffort;

        public ContestStats ContestStat;

        #endregion


        #region Emotion info

        public byte Fullness;
        public byte Enjoyment;

        public byte NotOTFriendship;
        public byte NotOTAffection;
        public byte NotOTMemoryIntensity;
        public byte NotOTMemoryLine;
        public byte NotOTMemoryFeeling;

        public ushort NotOTMemoryTextVar;

        public byte OTFriendship;
        public byte OTAffection;
        public byte OTMemoryIntensity;
        public byte OTMemoryLine;
        public ushort OTMemoryTextVar;
        public byte OTMemoryFeeling;

        #endregion


        public void Attack(Pokemon opponent, MoveBattle move)
        {
            switch (move)
            {
                case MoveBattle.Move_1:
                    if (Move_1 != null)
                    {
                        // -- Do attack
                        Move_1.Left--;
                    }
                    break;

                case MoveBattle.Move_2:
                    if (Move_2 != null)
                    {
                        // -- Do attack
                        Move_2.Left--;
                    }
                    break;

                case MoveBattle.Move_3:
                    if (Move_3 != null)
                    {
                        // -- Do attack
                        Move_3.Left--;
                    }
                    break;

                case MoveBattle.Move_4:
                    if (Move_4 != null)
                    {
                        // -- Do attack
                        Move_4.Left--;
                    }
                    break;
            }
        }

        public void Attack(Pokemon opponent_1, Pokemon opponent_2) { }

        public void UseItem(Item item) { }

        public void FilterPokemon()
        {
            ExperiencePoints = 0;
            Abilities = null;

            Move_1 = new Attack();
            Move_2 = new Attack();
            Move_3 = new Attack();
            Move_4 = new Attack();
        }
    }

    public class Item
    {
        public static Item FromReader(IProtocolDataReader reader) { return null; }

        public void ToStream(IProtocolStream stream) { }
    }



    public class PokemonGen6
    {
        #region Block A

        public ushort PokedexID;

        public ushort HeldItem;

        public ushort OTID;
        public ushort OTSecretID;

        public uint ExperiencePoints;

        public byte Ability;
        public byte AbilityNumber;

        public ushort HitsRemainingOnTrainingBag;

        public utri PID;

        public byte Nature;

        public byte Flags; // -- Bit 0 - Fateful Encounter Flag, Bit 1 - Female, Bit 2 - Genderless, Bit 3-7 - Alternate Forms

        public byte HPEffortValue;
        public byte AttackEffortValue;
        public byte DefenseEffortValue;
        public byte SpeedEffortValue;
        public byte SPAttackEffortValue;
        public byte SPDefenseEffortValue;

        public byte ContestStatCool;
        public byte ContestStatBeauty;
        public byte ContestStatCute;
        public byte ContestStatSmart;
        public byte ContestStatTough;
        public byte ContestStatSheen;

        public byte Markings;
        public byte Pokérus;

        public uint SuperTraining;

        public uhex Ribbons;

        //public ushort Unused;

        public byte ContestMemoryRibbon;
        public byte BattleMemoryRibbon;
        //public uhex Unused;

        #endregion Block A

        #region Block B

        public pokestring Nickname;

        //public ushort NullTerminator;

        public ushort Move_1_ID;
        public ushort Move_2_ID;
        public ushort Move_3_ID;
        public ushort Move_4_ID;

        public byte Move_1_CurrentPP;
        public byte Move_2_CurrentPP;
        public byte Move_3_CurrentPP;
        public byte Move_4_CurrentPP;

        public uint MovePPUps;

        public ushort RelearnMove_1_ID;
        public ushort RelearnMove_2_ID;
        public ushort RelearnMove_3_ID;
        public ushort RelearnMove_4_ID;

        public byte SuperTrainingFlag; // -- 0 - Missions Unavailable, 1 - Missions Available


        //public byte Unused;

        public uint Stats;

        #endregion Block B

        #region Block C

        public pokestring LatestNotOTHandler;

        //public ushort NullTerminator;

        public byte NotOTGender; // --  0 - Male, 1 - Female

        public byte CurrentHandler;  // -- 0 - OT, 1 - NotOT

        public ushort Geolocation_1;
        public ushort Geolocation_2;
        public ushort Geolocation_3;
        public ushort Geolocation_4;
        public ushort Geolocation_5;

        //public ushort Unused;
        //public ushort Unused;

        public byte NotOTFriendship;
        public byte NotOTAffection;
        public byte NotOTMemoryIntensity;
        public byte NotOTMemoryLine;
        public byte NotOTMemoryFeeling;

        //public byte Unused;

        public ushort NotOTMemoryTextVar;

        //public ushort Unused;
        //public ushort Unused;

        public byte Fullness;
        public byte Enjoyment;

        #endregion Block C

        #region Block D

        public pokestring OTName;

        //public ushort NullTerminator;

        public byte OTFriendship;
        public byte OTAffection;
        public byte OTMemoryIntensity;
        public byte OTMemoryLine;
        public ushort OTMemoryTextVar;
        public byte OTMemoryFeeling;

        public utri DateEggReceived;
        public utri DateMet;

        public byte UnknownUnused;

        public ushort EggLocation;

        public ushort MetAtLocation;

        public byte Pokéball;

        public byte EnciunterFlag; // -- Bit 0-6 - Encounter Level, Bit 7 - Female OT Gender
        public byte EncounterType;

        public byte OTGameID;

        public byte CountryID;

        public byte RegionID;

        public byte _3DSRegionID;

        public byte OTLanguageID;

        //public uint Unused;

        #endregion Block D
    }

    public class PokemonMy
    {
        public int ID;

        public byte Level;
        public ulong Experience;

        public PokemonType[] Types;

        public EggType[] EggTypes;

        public Ability[] Abilities;

        public Attack[] Attacks;

        public void Attack(Pokemon opponent, Move move)
        {
            for (int i = 0; i < Attacks.Length; i++)
            {
                if (move == Attacks[i].Move)
                    Attacks[i].Left--;
            }
        }

        public void Attack(Pokemon opponent_1, Pokemon opponent_2) { }

        public void UseItem(Item item) { }

        public void FilterPokemon()
        {
            Experience = 0;
            Abilities = null;
            Attacks = null;
        }
    }
    
    /// <summary>
    /// Unigned 3 byte number
    /// </summary>
    public struct utri
    {
        private readonly byte[] _bytes;

        public utri(uint value)
        {
            _bytes = new byte[]
            {
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF >> 0)
            };
        }

        public static implicit operator utri(uint value)
        {
            return new utri(value);
        }

        public static implicit operator uint(utri value)
        {
            return (uint) ((value._bytes[2] << 0) | (value._bytes[1] << 8) | (value._bytes[0] << 16));
        }
    }

    /// <summary>
    /// Unigned 6 byte number
    /// </summary>
    public struct uhex
    {
        private readonly byte[] _bytes;

        public uhex(ulong value)
        {
            _bytes = new byte[]
            {
                (byte)((value & 0xFF0000000000) >> 40),
                (byte)((value & 0xFF00000000) >> 32),
                (byte)((value & 0xFF000000) >> 24),
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF >> 0)
            };
        }

        public static implicit operator uhex(ulong value)
        {
            return new uhex(value);
        }

        public static implicit operator ulong(uhex value)
        {
            return (uint)((value._bytes[2] << 0) | (value._bytes[1] << 8) | (value._bytes[0] << 16) | (value._bytes[0] << 24) | (value._bytes[0] << 32) | (value._bytes[0] << 40));
        }
    }

    public struct pokestring {}
}
