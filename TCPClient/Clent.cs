using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;
using ClassLibrary;

namespace TCPClient
{
    internal class Client
    {
        static void Main(string[] args)
        {
            #region Povezivanje
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50001);

            try
            {
                clientSocket.Connect(serverEP);
                Console.WriteLine("Klijent je povezan na server!");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška prilikom povezivanja: {ex.Message}");
                return;
            }
            #endregion

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

            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, ispitanik);
                byte[] data = ms.ToArray();
                clientSocket.Send(data);
                Console.WriteLine("Podaci o ispitaniku su poslati serveru.");
            }
            #endregion

            #region Prijem trajanja eksperimenta (Polling model)
            byte[] trajanjeData = new byte[4];
            bool trajanjePrimljeno = false;

            while (!trajanjePrimljeno)
            {
                // Proveravamo da li su podaci spremni za čitanje
                if (clientSocket.Poll(1000000, SelectMode.SelectRead)) // Čekanje do 1 sekunde
                {
                    int received = clientSocket.Receive(trajanjeData);
                    if (received > 0)
                    {
                        int trajanjeEksperimenta = BitConverter.ToInt32(trajanjeData, 0);
                        Console.WriteLine($"Trajanje eksperimenta primljeno: {trajanjeEksperimenta} sekundi");

                        // Pokretanje simulacije
                        SimulacijaEksperimenta(clientSocket, formatter, trajanjeEksperimenta);
                        trajanjePrimljeno = true;
                    }
                }
                else
                {
                    Console.WriteLine("Čekam da server pošalje trajanje eksperimenta...");
                }
            }
            #endregion

            #region Zatvaranje
            Console.WriteLine("Klijent završava sa radom.");
            clientSocket.Close();
            #endregion
        }

        static void SimulacijaEksperimenta(Socket clientSocket, BinaryFormatter formatter, int trajanjeEksperimenta)
        {
            Random random = new Random();
            Stopwatch ukupanTajmer = Stopwatch.StartNew();

            while (ukupanTajmer.Elapsed.TotalSeconds < trajanjeEksperimenta)
            {
                string simbol = random.Next(2) == 0 ? "X" : "O";
                Console.WriteLine($"Prikazan simbol: {simbol}");

                Stopwatch stopwatch = Stopwatch.StartNew();
                string pritisnutiSimbol = "Ignorisano";
                bool tacno = (simbol == "X");

                while (stopwatch.ElapsedMilliseconds < 1000)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Spacebar)
                        {
                            pritisnutiSimbol = "O";
                            tacno = (simbol == "O");
                            break;
                        }
                    }
                }

                stopwatch.Stop();

                Dogadjaj dogadjaj = new Dogadjaj
                {
                    PrikazaniSimbol = simbol,
                    ReakcionoVreme = stopwatch.Elapsed.TotalSeconds,
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
            }

            Console.WriteLine("Eksperiment završen.");
        }
    }
}
