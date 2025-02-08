using System.Net.Sockets;

namespace GMS_CSharp_Server
{
    public class SocketHelper
        {
            Queue<BufferStream> WriteQueue = new Queue<BufferStream>();
            public Thread ReadThread;
            public Thread WriteThread;
            public Thread AbortThread;
            public System.Net.Sockets.TcpClient MscClient;
            public Server ParentServer;
            public string ClientIPAddress;
            public string ClientName;
            public string ClientDeck;
            public int ClientNumber;
            public Lobby GameLobby;
            public bool IsSearching;
            public bool IsIngame;
            int BufferSize = Server.BufferSize;
            int BufferAlignment = Server.BufferAlignment;
            public int HandSize = 0;
            public String TalentCard = "undefined";
            /// <summary>
            /// Starts the given client in two threads for reading and writing.
            /// </summary>
            public void StartClient(TcpClient client, Server server)
            {
                //Sets client variable.
                MscClient = client;
                MscClient.SendBufferSize = BufferSize;
                MscClient.ReceiveBufferSize = BufferSize;
                ParentServer = server;

                //Starts a read thread.
                ReadThread = new Thread(new ThreadStart(delegate
                {
                    Read(client);
                }));
                ReadThread.Start();
                Console.WriteLine("Client read thread started.");

                //Starts a write thread.
                WriteThread = new Thread(new ThreadStart(delegate
                {
                    Write(client);
                }));
                WriteThread.Start();
                Console.WriteLine("Client write thread started.");
            }

            /// <summary>
            /// Sends a string message to the client. This message is added to the write queue and send
            /// once it is it's turn. This ensures all messages are send in order they are given.
            /// </summary>
            public void SendMessage(BufferStream buffer)
            {
                WriteQueue.Enqueue(buffer);
            }

            /// <summary>
            /// Disconnects the client from the server and stops all threads for client.
            /// </summary>
            public void DisconnectClient()
            {
                //Console Message.
                Console.WriteLine("Disconnecting: " + ClientIPAddress);

                //Check if client is ingame.
                if (IsIngame)
                {
                    //Find opposing client.
                    try
                    {
                        SocketHelper opponet = null;
                        foreach (SocketHelper lobbyClient in GameLobby.LobbyClients)
                        {
                            if (lobbyClient != this)
                            {
                                opponet = lobbyClient;
                            }
                        }
                        
                        //Causes opponent to win.
                        BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                        buffer.Seek(0);
                        UInt16 constant_out = 1010;
                        buffer.Write(constant_out);
                        opponet.SendMessage(buffer);
                        Console.WriteLine(ClientIPAddress + " is ingame. Granting win to opponent.");

                        //Remove lobby from server.
                        ParentServer.Lobbies.Remove(GameLobby);
                        GameLobby = null;
                        IsIngame = false;
                    }
                    catch (System.Exception)
                    {
                        
                    }
                }

                //Removes client from server.
                ParentServer.Clients.Remove(this);
                if (IsSearching)
                {
                    Console.WriteLine(ClientIPAddress + " was searching for a game. Stopped searching.");
                    ParentServer.SearchingClients.Remove(this);
                    IsSearching = false;
                }

                //Closes Stream.
                MscClient.Close();

                //Starts an abort thread.
                AbortThread = new Thread(new ThreadStart(delegate
                {
                    Abort();
                }));
                Console.WriteLine("Aborting threads on client.");
                AbortThread.Start();
            }

            /// <summary>
            /// Handles aborting of threads.
            /// </summary>
            public void Abort()
            {
                try
                {
                    //Stops Threads
                    ReadThread.Interrupt();
                    //Console.WriteLine("Read thread aborted on client.");
                    WriteThread.Interrupt();
                    //Console.WriteLine("Write thread aborted on client.");
                    Console.WriteLine(ClientName + " disconnected.");
                    Console.WriteLine(Convert.ToString(ParentServer.Clients.Count) + " clients online.");
                    AbortThread.Interrupt();
                }
                catch (System.Exception)
                {
                    Console.WriteLine("Some error ocurred on user disconnect");
                }
            }

            /// <summary>
            /// Writes data to the client in sequence on the server.
            /// </summary>
            public void Write(TcpClient client)
            {
                while (true)
                {
                    Thread.Sleep(10);
                    if (WriteQueue.Count != 0)
                    {
                        try
                        {
                            BufferStream buffer = WriteQueue.Dequeue();
                            NetworkStream stream = client.GetStream();
                            stream.Write(buffer.Memory, 0, buffer.Iterator);
                            stream.Flush();
                        }
                        catch (System.IO.IOException)
                        {
                            DisconnectClient();
                            break;
                        }
                        catch (NullReferenceException)
                        {
                            DisconnectClient();
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }
                    }
                }
            }

            /// <summary>
            /// Reads data from the client and sends back a response.
            /// </summary>
            public void Read(TcpClient client)
            {
                while (true)
                {
                    try
                    {
                        Thread.Sleep(10);
                        BufferStream readBuffer = new BufferStream(BufferSize, 1);
                        NetworkStream stream = client.GetStream();
                        stream.Read(readBuffer.Memory, 0, BufferSize);

                        //Read the header data.
                        ushort constant;
                        readBuffer.Read(out constant);

                        //Determine input commmand.
                        switch (constant)
                        {
                            case 1:
                            {
                                var lobby = new Lobby();
                                String lname = "";
                                readBuffer.Read(out lname);
                                lobby.SetupLobby(lname, this);
                                ParentServer.Lobbies.Add(lobby);
                                Console.WriteLine("Created Lobby " + lobby.Name);
                                BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                buffer.Seek(0);
                                UInt16 constant_out = 1;
                                buffer.Write(constant_out);
                                buffer.Write(GameLobby.Name);
                                SendMessage(buffer);
                                break;
                            }
                            case 2:
                            {
                                //Send Lobby list to client.
                                BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                buffer.Seek(0);
                                UInt16 constant_out = 2;
                                buffer.Write(constant_out);
                                String LobbyNames = "";
                                foreach (var lobby in ParentServer.Lobbies)
                                {
                                    if (lobby.Name != null)
                                    {
                                        LobbyNames += lobby.Name + ";";
                                    }                                    
                                }
                                buffer.Write(LobbyNames);
                                SendMessage(buffer);
                                Console.WriteLine("Sent Lobby list to client");
                                break;
                            }
                            case 4:
                            {
                                String lobby_name;
                                readBuffer.Read(out lobby_name);
                                foreach (var lobby in ParentServer.Lobbies)
                                {
                                    if (lobby.Name.Equals(lobby_name))
                                    {
                                        Console.WriteLine(ClientName + " Joined " + lobby_name);
                                        lobby.AddClient(this);
                                    }
                                }
                                break;
                            }
                            case 5:
                            {
                                readBuffer.Read(out TalentCard);
                                BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                buffer.Seek(0);
                                UInt16 constant_out = 5;
                                buffer.Write(constant_out);
                                buffer.Write(TalentCard);
                                Console.WriteLine(ClientName + ":" + ClientNumber.ToString() + " set talent to " + TalentCard);
                                foreach (var other in GameLobby.LobbyClients)
                                {
                                    if (ClientName != other.ClientName)
                                    {
                                        other.SendMessage(buffer);
                                    }
                                }                
                                break;
                            }
                            case 6:
                            {
                                String PlayedCard;
                                readBuffer.Read(out PlayedCard);
                                BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                buffer.Seek(0);
                                UInt16 constant_out = 6;
                                buffer.Write(constant_out);
                                buffer.Write(PlayedCard);
                                Console.WriteLine(ClientName + ":" + ClientNumber.ToString() + " played " + PlayedCard);
                                foreach (var other in GameLobby.LobbyClients)
                                {
                                    if (ClientName != other.ClientName)
                                    {
                                        other.SendMessage(buffer);
                                    }
                                }                
                                break;
                            }
                            //New Connection
                            case 2000:
                                {
                                    //Read out client data.
                                    String name;
                                    readBuffer.Read(out name);

                                    //Update client information.
                                    ClientName = name;

                                    //Console Message.
                                    Console.WriteLine(name + " connected.");
                                    Console.WriteLine(Convert.ToString(ParentServer.Clients.Count) + " clients online.");
                                    break;
                                }

                            //Find Game
                            case 8://FindGame
                                {
                                    IsSearching = true;
                                    IsIngame = false;

                                    //Add client to searching clients.
                                    ParentServer.SearchingClients.Add(this);
                                    Console.WriteLine(ClientName + " is searching for a game");
                                    break;
                                }

                            //Cancel Find Game
                            case 2002:
                                {
                                    //Read out client data.
                                    String ip;
                                    readBuffer.Read(out ip);

                                    //Update client information.
                                    IsSearching = false;

                                    //Removes client from searching list.
                                    ParentServer.SearchingClients.Remove(this);
                                    Console.WriteLine(ip + " stopped searching.");
                                    break;
                                }

                            //Recive Move Input
                            case 2003:
                                {
                                    //Read buffer data.
                                    String name;
                                    UInt32 input;
                                    UInt16 xx;
                                    UInt16 yy;
                                    readBuffer.Read(out name);
                                    readBuffer.Read(out input);
                                    readBuffer.Read(out xx);
                                    readBuffer.Read(out yy);

                                    //Send start game to clients.
                                    BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                    buffer.Seek(0);
                                    UInt16 constant_out = 1004;
                                    buffer.Write(constant_out);
                                    buffer.Write(name);
                                    buffer.Write(input);
                                    buffer.Write(xx);
                                    buffer.Write(yy);
                                    ParentServer.SendToLobby(GameLobby, buffer);
                                    Console.WriteLine("Recived input at " + Convert.ToString(xx) + "," + Convert.ToString(yy) + " from " + ClientIPAddress);
                                    break;
                                }

                            //Recive client ping.
                            case 2004:
                                {
                                    //Send ping return to client.
                                    BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                    buffer.Seek(0);
                                    UInt16 constant_out = 1050;
                                    buffer.Write(constant_out);
                                    SendMessage(buffer);
                                    break;
                                }

                            //Recive server ping.
                            case 2005:
                                {
                                    //Nothing - Ping handled in ping thread.
                                    break;
                                }

                            //Recive matchmaking players request.
                            case 2006:
                                {
                                    //Send players online return to client.
                                    BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                    buffer.Seek(0);
                                    UInt16 constant_out = 1008;
                                    int players_online = ParentServer.Clients.Count;
                                    buffer.Write(constant_out);
                                    buffer.Write(players_online);
                                    SendMessage(buffer);
                                    break;
                                }

                            // 7 = Recive End Turn
                            case 2007:
                                {
                                    //Send end turn input to clients.
                                    BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                                    buffer.Seek(0);
                                    UInt16 constant_out = 1006;
                                    buffer.Write(constant_out);
                                    ParentServer.SendToLobby(GameLobby, buffer);
                                    Console.WriteLine("Recived end turn from " + ClientIPAddress);
                                    break;
                                }
                        }
                    }
                    catch (System.IO.IOException)
                    {
                        DisconnectClient();
                        break;
                    }
                    catch (NullReferenceException)
                    {
                        DisconnectClient();
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        //Do nothing.
                        break;
                    }
                    catch (InvalidOperationException)
                    {
                        //Do nothing.
                        break;
                    }
                }
            }
        }
}