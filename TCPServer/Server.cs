using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using ClassLibrary;

namespace TCPServer
{
    internal class Server
    {
        static void Main(string[] args)
        {
            #region Povezivanje
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 50001);
            serverSocket.Bind(serverEP);
            serverSocket.Listen(1);

            Console.WriteLine($"Server je spreman na {serverEP}");
            Socket clientSocket = serverSocket.Accept();
            Console.WriteLine($"Povezan klijent: {clientSocket.RemoteEndPoint}");
            #endregion

            BinaryFormatter formatter = new BinaryFormatter();
            List<Dogadjaj> dogadjaji = new List<Dogadjaj>();

            #region Prijem podataka o ispitaniku
            try
            {
                byte[] buffer = new byte[1024];
                clientSocket.Receive(buffer);

                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    Ispitanik ispitanik = (Ispitanik)formatter.Deserialize(ms);
                    Console.WriteLine($"Ispitanik prijavljen: {ispitanik.Ime} {ispitanik.Prezime}");
                }

                // Slanje trajanja eksperimenta
                Console.Write("Unesite trajanje eksperimenta (u sekundama): ");
                int trajanjeEksperimenta = int.Parse(Console.ReadLine());
                byte[] trajanjeData = BitConverter.GetBytes(trajanjeEksperimenta);
                clientSocket.Send(trajanjeData);
                Console.WriteLine("Trajanje eksperimenta poslato klijentu.");

                // Početak prikupljanja događaja
                Console.WriteLine("Čekanje podataka o događajima...");
                PrimiDogadjaje(clientSocket, formatter, dogadjaji);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška: {ex.Message}");
                clientSocket.Close();
            }
            #endregion

            #region Zatvaranje
            Console.WriteLine("Server završava sa radom.");
            clientSocket.Close();
            serverSocket.Close();
            #endregion
        }

        static void PrimiDogadjaje(Socket clientSocket, BinaryFormatter formatter, List<Dogadjaj> dogadjaji)
        {
            while (true)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int receivedBytes = clientSocket.Receive(buffer);
                    if (receivedBytes == 0) break; // Klijent zatvorio vezu

                    using (MemoryStream ms = new MemoryStream(buffer))
                    {
                        Dogadjaj dogadjaj = (Dogadjaj)formatter.Deserialize(ms);
                        dogadjaji.Add(dogadjaj);

                        Console.WriteLine($"Primljen događaj: {dogadjaj.PrikazaniSimbol}, " +
                                          $"Reakcija: {dogadjaj.PritisnutiSimbol}, " +
                                          $"Reakciono vreme: {dogadjaj.ReakcionoVreme:F2} sekunde, " +
                                          $"Tačnost: {dogadjaj.Tacnost}");
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Greška prilikom prijema događaja: {ex.Message}");
                    break;
                }
            }

            Console.WriteLine("Prijem događaja završen.");
            ObradiStatistiku(dogadjaji);
        }

        static void ObradiStatistiku(List<Dogadjaj> dogadjaji)
        {
            double ukupnoVreme = 0;
            double minimalnoVreme = double.MaxValue;
            int tacniOdgovori = 0;
            int lazniPozitivi = 0;
            int lazniNegativi = 0;

            foreach (var dogadjaj in dogadjaji)
            {
                ukupnoVreme += dogadjaj.ReakcionoVreme;
                if (dogadjaj.ReakcionoVreme < minimalnoVreme)
                {
                    minimalnoVreme = dogadjaj.ReakcionoVreme;
                }

                if (dogadjaj.Tacnost)
                {
                    tacniOdgovori++;
                }
                else if (dogadjaj.PrikazaniSimbol == "X" && dogadjaj.PritisnutiSimbol == "O")
                {
                    lazniPozitivi++;
                }
                else if (dogadjaj.PrikazaniSimbol == "O" && dogadjaj.PritisnutiSimbol == "Ignorisano")
                {
                    lazniNegativi++;
                }
            }

            double prosecnoVreme = ukupnoVreme / dogadjaji.Count;
            double tacnost = (double)tacniOdgovori / dogadjaji.Count * 100;

            Console.WriteLine("\nRezultati:");
            Console.WriteLine($"Prosečno reakciono vreme: {prosecnoVreme:F2} sekunde");
            Console.WriteLine($"Minimalno reakciono vreme: {minimalnoVreme:F2} sekunde");
            Console.WriteLine($"Tačnost: {tacnost:F2}%");
            Console.WriteLine($"Stopa lažnih pozitiva: {lazniPozitivi}");
            Console.WriteLine($"Stopa lažnih negativa: {lazniNegativi}");
        }
    }
}
