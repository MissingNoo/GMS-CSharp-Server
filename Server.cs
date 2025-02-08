﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace GMS_CSharp_Server
{
    public class Server
    {
        public List<Lobby> Lobbies;
        public List<SocketHelper> Clients;
        public List<SocketHelper> SearchingClients;
        public Thread TCPThread;
        public Thread PingThread;
        public Thread MatchmakingThread;
        public TcpListener TCPListener = null;

        public const int BufferAlignment = 1;
        public const int BufferSize = 256;
        private readonly string[] Maps = { "Open#Plains", "Riverside", "Lonely#Mountain", "Four#Islands", "Twin#Oasis", "Pyramid#Grounds", "Canyon#Valley", "Stone#Arena", "Caverns", "Sister#Caves", "Frozen#Lake", "Melting#Snow" };
        
        /// <summary>
        /// Starts the server.
        /// </summary>
        public void StartServer(int tcpPort)
        {
            //Creates a client list.
            Clients = new List<SocketHelper>();
            Lobbies = new List<Lobby>();
            SearchingClients = new List<SocketHelper>();

            //Starts a listen thread to listen for connections.
            TCPThread = new Thread(new ThreadStart(delegate
            {
                Listen(tcpPort);
            }));
            TCPThread.Start();
            Console.WriteLine("Listen thread started.");

            //Starts a ping thread to keep connection alive.
            PingThread = new Thread(new ThreadStart(delegate
            {
                Ping();
            }));
            PingThread.Start();
            Console.WriteLine("Ping thread started.");

            //Starts a matchmaking thread to create lobbies.
            MatchmakingThread = new Thread(new ThreadStart(delegate
            {
                Matchmaking();
            }));
            MatchmakingThread.Start();
            Console.WriteLine("Matchmaking thread started.");
        }

        /// <summary>
        /// Stops the server from running.
        /// </summary>
        public void StopServer()
        {
            TCPListener.Stop();

            TCPThread.Interrupt();
            PingThread.Interrupt();
            MatchmakingThread.Interrupt();

            foreach (SocketHelper client in Clients)
            {
                client.MscClient.GetStream().Close();
                client.MscClient.Close();
                client.ReadThread.Interrupt();
                client.WriteThread.Interrupt();
            }

            Clients.Clear();
            Lobbies.Clear();
        }

        /// <summary>
        /// Constantly pings clients with messages to see if they disconnect.
        /// </summary>
        private void Ping()
        {
            //Send ping to clients every 3 seconds.
            while (true)
            {
                Thread.Sleep(3000);
                BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                buffer.Seek(0);
                ushort constant_out = 1007;
                buffer.Write(constant_out);
                SendToAllClients(buffer);
            }
        }

        /// <summary>
        /// Sends a message out to all connected clients.
        /// </summary>
        public void SendToAllClients(BufferStream buffer)
        {
            foreach (SocketHelper client in Clients)
            {
                client.SendMessage(buffer);
            }
        }

        /// <summary>
        /// Sends a message out all clients in a lobby.
        /// </summary>
        public void SendToLobby(Lobby lobby, BufferStream buffer)
        {
            foreach (SocketHelper client in lobby.LobbyClients)
            {
                client.SendMessage(buffer);
            }
        }

        /// <summary>
        /// Listens for clients and starts threads to handle them.
        /// </summary>
        private void Listen(int port)
        {
            TCPListener = new TcpListener(IPAddress.Any, port);
            TCPListener.Start();

            while (true)
            {
                Thread.Sleep(10);
                TcpClient tcpClient = TCPListener.AcceptTcpClient();
                Console.WriteLine("New client detected. Connecting client.");
                SocketHelper helper = new SocketHelper();
                helper.StartClient(tcpClient, this);
                Clients.Add(helper);
            }
        }

        /// <summary>
        /// Handles matchmaking between clients searching for games.
        /// </summary>
        public void Matchmaking()
        {

            while (true)
            {
                Thread.Sleep(10);
                bool match_made = false;
                bool should_break = false;
                SocketHelper client_to_remove_1 = null;
                SocketHelper client_to_remove_2 = null;

                //Finds a match for clients.
                foreach (SocketHelper client1 in SearchingClients)
                {
                    //Finds a match for clients.
                    foreach (SocketHelper client2 in SearchingClients)
                    {
                        if (client1.ClientName != client2.ClientName)
                        {
                            //Create a lobby.
                            Lobby lobby = new Lobby();
                            lobby.SetupLobby(null, client1, client2);
                            Lobbies.Add(lobby);

                            //Remove clients from searching list.
                            client1.IsSearching = false;
                            client2.IsSearching = false;
                            client_to_remove_1 = client1;
                            client_to_remove_2 = client2;
                            should_break = true;
                            match_made = true;

                            //Send start game to clients.
                            BufferStream buffer = new BufferStream(BufferSize, BufferAlignment);
                            buffer.Seek(0);
                            UInt16 constant = 7;//StartGame
                            buffer.Write(constant);
                            foreach (var client in lobby.LobbyClients)
                            {
                                client.SendMessage(buffer);
                            }
                            //SendToLobby(lobby, buffer);
                            string name_1 = client1.ClientName;
                            string name_2 = client2.ClientName;
                            Console.WriteLine("Matchmade between " + name_1 + " and " + name_2);
                            break;
                        }
                    }

                    //Check if match have been made.
                    if (should_break)
                    {
                        break;
                    }
                }

                //Check if match was made.
                if (match_made)
                {
                    SearchingClients.Remove(client_to_remove_1);
                    SearchingClients.Remove(client_to_remove_2);
                }
            }
        }


        
        /// <summary>
        /// Handles clients. Reads and writes data and stores client information.
        /// </summary>
        }
}