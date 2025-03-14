﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Ejercicio6ServidoresJuego
{
    internal class GameServer
    {
        private readonly int initialPort = 31416;
        private readonly int finalPort = 65535;
        private int port;
        private Socket serverSocket;
        private readonly List<Socket> clients = new List<Socket>();
        private readonly Dictionary<Socket, int> clientNumbers = new Dictionary<Socket, int>();
        private static readonly Random random = new Random();
        private bool gameStarted = false;
        private Timer timer;
        private int countdown = 20;

        private readonly object keyObject = new object();

        public void Init()
        {
            try
            {
                port = LoadPort();
                if (port == -1)
                {
                    Console.WriteLine("[DEBUG] No available port found.");
                    return;
                }

                IPEndPoint ie = new IPEndPoint(IPAddress.Any, port);
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                serverSocket.Bind(ie);
                serverSocket.Listen(10);

                Console.WriteLine($"[DEBUG] Server listening on port {port}");

                timer = new Timer(1000);
                timer.Elapsed += Countdown;
                timer.Start();

                while (!gameStarted)
                {
                    Socket clientSocket = serverSocket.Accept();
                    clients.Add(clientSocket);
                    Thread clientThread = new Thread(() => HandleClient(clientSocket));
                    clientThread.IsBackground = true;
                    clientThread.Start();
                }

                timer.Stop();
                DetermineWinner();
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ArgumentException || e is ArgumentNullException)
            {
                Console.WriteLine($"[DEBUG] {e.Message}");
            }
        }

        private void Countdown(object sender, ElapsedEventArgs e)
        {
            lock (keyObject)
            {
                if (countdown == 0)
                {
                    gameStarted = true;
                    timer.Stop();
                    Console.WriteLine("[DEBUG] Game starting...");
                    DetermineWinner();
                    return;
                }

                Console.WriteLine($"[DEBUG] {countdown} seconds left to join.");

                // Lista de clientes a eliminar después de la iteración
                List<Socket> disconnectedClients = new List<Socket>();


                foreach (Socket clientSocket in clients)
                {
                    try
                    {
                        if (!clientSocket.Connected)
                        {
                            disconnectedClients.Add(clientSocket); // lo marco el cliente para eliminar después
                            //aqui podria ahcer esto???
                            continue; // Saltar al siguiente cliente
                        }

                        using (NetworkStream network = new NetworkStream(clientSocket))
                        using (StreamWriter writer = new StreamWriter(network) { AutoFlush = true })
                        {
                            writer.WriteLine($"[SERVER] {countdown} Seconds left to join the game.");
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ArgumentException || ex is ArgumentNullException)
                    {
                        Console.WriteLine($"[DEBUG] Error sending time message to client: {ex.Message}");
                    }
                }

                foreach (Socket clientSocket in disconnectedClients)
                {
                    lock (keyObject)
                    {
                        clients.Remove(clientSocket);
                        clientNumbers.Remove(clientSocket);
                        clientSocket.Close();
                        Console.WriteLine("[DEBUG] Client has disconnected.");
                    }
                }

                countdown--;
            }
        }


        private void HandleClient(Socket clientSocket)
        {
            try
            {
                using (NetworkStream network = new NetworkStream(clientSocket))
                using (StreamReader sr = new StreamReader(network))
                using (StreamWriter writer = new StreamWriter(network) { AutoFlush = true }) // ASI evit hacer `Flush()` manualmente
                {
                    writer.WriteLine("[SERVER] Waiting for other players...");

                    int assignedNumber = random.Next(1, 21);
                    lock (keyObject)
                    {
                        clientNumbers[clientSocket] = assignedNumber;
                    }

                    writer.WriteLine($"[SERVER] Your number: {assignedNumber}");
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ArgumentException || e is ArgumentNullException)
            {
                Console.WriteLine($"[DEBUG] {e.Message}");
                lock (keyObject)
                {
                    clients.Remove(clientSocket);
                    clientNumbers.Remove(clientSocket);
                }
                clientSocket.Close();
            }

        }

        private void DetermineWinner()
        {
            lock (keyObject)
            {
                if (clientNumbers.Count == 0)
                {
                    Console.WriteLine("[DEBUG] No clients connected. Ending game.");
                    return;
                }
                KeyValuePair<Socket, int> winner = clientNumbers.OrderByDescending(kvp => kvp.Value).First();
                Console.WriteLine($"[DEBUG] The winner is a client with number {winner.Value}");

                foreach (KeyValuePair<Socket, int> kvp in clientNumbers)
                {
                    try
                    {
                        using (NetworkStream network = new NetworkStream(kvp.Key))
                        using (StreamWriter writer = new StreamWriter(network) { AutoFlush = true })
                        {
                            if (kvp.Key == winner.Key)
                            {
                                writer.WriteLine("[SERVER] You are the winner!");
                            }
                            else
                            {
                                writer.WriteLine($"[SERVER] You lost. Winning number: {winner.Value}");
                            }
                        }
                    }
                    catch (Exception e) when (e is SocketException || e is ArgumentException || e is ArgumentNullException || e is IOException)
                    {
                        Console.WriteLine($"[DEBUG] Error sending message to client: {e.Message}");
                    }

                    kvp.Key.Close();
                }

                serverSocket.Close();
            }
        }

        private int LoadPort()
        {
            for (int i = initialPort; i < finalPort; i++)
            {
                if (CheckPortAvailable(i))
                {
                    return i;
                }
            }
            return -1;
        }

        private bool CheckPortAvailable(int port)
        {
            try
            {
                using (Socket testSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    testSocket.Bind(new IPEndPoint(IPAddress.Any, port));
                }
                return true;
            }
            catch (Exception e) when (e is SocketException || e is IOException || e is ArgumentException)
            {
                return false;
            }
        }
    }
}
