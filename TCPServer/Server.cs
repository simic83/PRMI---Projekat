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
            #region Inicijalizacija i povezivanje
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 50001);
            serverSocket.Bind(serverEP);
            serverSocket.Listen(5);

            Console.WriteLine($"Server je spreman na {serverEP}");
            Socket acceptedSocket = serverSocket.Accept();
            Console.WriteLine($"Povezan klijent: {acceptedSocket.RemoteEndPoint}");
            #endregion

            BinaryFormatter formatter = new BinaryFormatter();
            Ispitanik ispitanik = null;
            List<Dogadjaj> dogadjaji = new List<Dogadjaj>();

            #region Prijem podataka
            try
            {
                byte[] buffer = new byte[1024];
                int receivedBytes = acceptedSocket.Receive(buffer);

                using (MemoryStream ms = new MemoryStream(buffer))
                {
                    ispitanik = (Ispitanik)formatter.Deserialize(ms);
                    Console.WriteLine($"Ispitanik prijavljen: {ispitanik.Ime} {ispitanik.Prezime}, ID: {ispitanik.ID}, Starost: {ispitanik.Starost}");
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška prilikom prijema podataka o ispitaniku: {ex.Message}");
            }

            while (true)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    int receivedBytes = acceptedSocket.Receive(buffer);
                    if (receivedBytes == 0) break;

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
                    Console.WriteLine($"Greška: {ex.Message}");
                    break;
                }
            }
            #endregion

            #region Obrada statistike
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
            #endregion

            #region Zatvaranje
            Console.WriteLine("Server završava sa radom.");
            acceptedSocket.Close();
            serverSocket.Close();
            #endregion
        }
    }
}
