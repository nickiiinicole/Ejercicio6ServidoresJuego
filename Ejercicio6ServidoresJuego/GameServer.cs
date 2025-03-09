using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using System.Threading;
using Timer = System.Timers.Timer;

namespace Ejercicio6ServidoresJuego
{
    internal class GameServer
    {
        private int initialPort = 31416;
        private int finalPort = 65535;
        private int port;
        private Socket serverSocket;
        private List<Socket> clients = new List<Socket>();
        private Dictionary<Socket, int> clientNumbers = new Dictionary<Socket, int>();
        private static Random random = new Random();
        private bool gameStarted = false;
        private Timer timer;
        private int countdown = 60;

        public void Init()
        {
            try
            {
                port = LoadPort();
                if (port == -1)
                {
                    Console.WriteLine("[DEBUG] No available port found");
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
            if (countdown == 0)
            {
                gameStarted = true;
                Console.WriteLine("[DEBUG] Game starting...");
                return;
            }

            Console.WriteLine($"[DEBUG] {countdown} seconds left to join.");
            countdown--;
        }

        private void HandleClient(Socket clientSocket)
        {
            try
            {
                using (NetworkStream network = new NetworkStream(clientSocket))
                using (StreamReader sr = new StreamReader(network))
                using (StreamWriter writer = new StreamWriter(network))
                {
                    writer.WriteLine("[SERVER] Waiting for other players...");
                    writer.Flush();

                    int assignedNumber = random.Next(1, 21);
                    clientNumbers[clientSocket] = assignedNumber;

                    writer.WriteLine($"[SERVER] Your number: {assignedNumber}");
                    writer.Flush();
                }
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ArgumentException || e is ArgumentNullException)
            {
                Console.WriteLine($"[DEBUG] {e.Message}");
            }
        }

        private void DetermineWinner()
        {
            var winner = clientNumbers.OrderByDescending(kvp => kvp.Value).First();
            Console.WriteLine($"[DEBUG] The winner is a client with number {winner.Value}");

            foreach (var kvp in clientNumbers)
            {
                using (NetworkStream network = new NetworkStream(kvp.Key))
                using (StreamWriter writer = new StreamWriter(network))
                {
                    if (kvp.Key == winner.Key)
                    {
                        writer.WriteLine("[SERVER] You are the winner!");
                    }
                    else
                    {
                        writer.WriteLine($"[SERVER] You lost. Winning number: {winner.Value}");
                    }
                    writer.Flush();
                }

                kvp.Key.Close();
            }

            serverSocket.Close();
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
            catch (Exception e) when (e is SocketException | e is IOException | e is ArgumentException)
            {
                return false;
            }
        }
    }
}
