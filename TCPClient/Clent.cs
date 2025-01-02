using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using System.Threading;
using ClassLibrary;

namespace TCPClient
{
    internal class Client
    {
        static void Main(string[] args)
        {
            #region Povezivanje
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                Blocking = false
            };

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50001);

            try
            {
                clientSocket.Connect(serverEP);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                Console.WriteLine("Povezivanje u toku...");
            }

            while (!clientSocket.Connected)
            {
                Thread.Sleep(100);
                if (clientSocket.Poll(0, SelectMode.SelectWrite))
                {
                    clientSocket.Connect(serverEP);
                }
            }

            Console.WriteLine("Klijent je povezan na server!");
            #endregion

            BinaryFormatter formatter = new BinaryFormatter();

            #region Login ispitanika
            Console.WriteLine("Unesite podatke o ispitaniku:");
            Console.Write("Ime: ");
            string ime = Console.ReadLine();
            Console.Write("Prezime: ");
            string prezime = Console.ReadLine();
            Console.Write("ID: ");
            string id = Console.ReadLine();
            Console.Write("Starost: ");
            int starost = int.Parse(Console.ReadLine());

            Ispitanik ispitanik = new Ispitanik
            {
                Ime = ime,
                Prezime = prezime,
                ID = id,
                Starost = starost
            };

            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, ispitanik);
                byte[] data = ms.ToArray();
                clientSocket.Send(data);
                Console.WriteLine("Podaci o ispitaniku su poslati serveru.");
            }
            #endregion

            #region Simulacija eksperimenta
            Random random = new Random();
            for (int i = 0; i < 10; i++) // 10 iteracija
            {
                string simbol = random.Next(2) == 0 ? "X" : "O"; // Nasumičan simbol
                Console.WriteLine($"Prikazan simbol: {simbol}");

                Stopwatch stopwatch = Stopwatch.StartNew(); // Početak merenja vremena reakcije
                string pritisnutiSimbol = "Ignorisano";
                bool tacno = simbol == "X";

                // Hvatanje reakcije unutar vremenskog ograničenja
                while (stopwatch.ElapsedMilliseconds < 2000) // Maksimalno 2 sekunde za reakciju
                {
                    if (Console.KeyAvailable) // Provera da li je pritisnut neki taster
                    {
                        var key = Console.ReadKey(true); // Čitanje tastera bez prikazivanja na konzoli
                        if (key.Key == ConsoleKey.Spacebar) // Provera da li je pritisnut Space
                        {
                            pritisnutiSimbol = "O";
                            tacno = simbol == "O"; // Tačno je samo ako je simbol bio "O"
                            break;
                        }
                    }
                }

                stopwatch.Stop(); // Završetak merenja vremena

                Dogadjaj dogadjaj = new Dogadjaj
                {
                    PrikazaniSimbol = simbol,
                    ReakcionoVreme = stopwatch.Elapsed.TotalSeconds, // Vreme u sekundama
                    PritisnutiSimbol = pritisnutiSimbol,
                    Tacnost = tacno
                };

                using (MemoryStream ms = new MemoryStream())
                {
                    formatter.Serialize(ms, dogadjaj);
                    byte[] data = ms.ToArray();
                    clientSocket.Send(data);
                    Console.WriteLine($"Reakcija poslata: {dogadjaj.PritisnutiSimbol}, Tačnost: {dogadjaj.Tacnost}");
                }

                Thread.Sleep(1000); // Pauza između prikaza simbola
            }
            #endregion

            #region Zatvaranje
            Console.WriteLine("Klijent završava sa radom.");
            clientSocket.Close();
            #endregion
        }
    }
}
