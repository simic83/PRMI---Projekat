using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using ClassLibrary;

namespace TCPServer
{
    internal class Server
    {
        static Dictionary<string, List<Dogadjaj>> sviDogadjaji = new Dictionary<string, List<Dogadjaj>>();
        static int brojKorisnika = 0;
        static int trajanjeEksperimenta = 0;
        static int trenutniKorisnici = 0;

        static void Main(string[] args)
        {
            #region Postavljanje broja korisnika
            Console.Write("Koliko korisnika radi eksperiment? ");
            brojKorisnika = int.Parse(Console.ReadLine());
            Console.Clear();
            Console.WriteLine($"Očekujemo povezivanje {brojKorisnika} korisnika...");
            #endregion

            #region Povezivanje
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 50001));
            serverSocket.Listen(10);
            Console.WriteLine($"Server je spreman na {serverSocket.LocalEndPoint} i čeka klijente...");

            List<Socket> klijenti = new List<Socket>();

            while (trenutniKorisnici < brojKorisnika)
            {
                if (serverSocket.Poll(2000 * 1000, SelectMode.SelectRead))
                {
                    Socket clientSocket = serverSocket.Accept();
                    klijenti.Add(clientSocket);
                    trenutniKorisnici++;

                    string klijentInfo = clientSocket.RemoteEndPoint.ToString();
                    Console.WriteLine($"Povezan klijent: {klijentInfo}");
                }
                else
                {
                    Console.WriteLine("Čekam nove klijente...");
                }
            }

            Console.Write("Unesite trajanje eksperimenta (u sekundama): ");
            trajanjeEksperimenta = int.Parse(Console.ReadLine());
            byte[] trajanjeData = BitConverter.GetBytes(trajanjeEksperimenta);

            foreach (var klijent in klijenti)
            {
                klijent.Send(trajanjeData);
                Console.WriteLine($"Trajanje eksperimenata poslato klijentu {klijent.RemoteEndPoint}");
            }
            #endregion

            #region Prikupljanje događaja
            List<Task> klijentskeNiti = new List<Task>();

            foreach (var klijent in klijenti)
            {
                klijentskeNiti.Add(Task.Run(() => ObradiKlijenta(klijent)));
            }

            Task.WaitAll(klijentskeNiti.ToArray());
            #endregion

            #region Generisanje izveštaja
            Console.WriteLine("\nSvi klijenti su završili eksperiment.");
            GenerisiIzvestaj(sviDogadjaji);
            #endregion
        }

        static void ObradiKlijenta(Socket clientSocket)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            string imeKlijenta = "";
            List<Dogadjaj> dogadjajiKlijenta = new List<Dogadjaj>();

            try
            {
                bool ispitanikPrimljen = false;

                while (true)
                {
                    if (clientSocket == null || !clientSocket.Connected) break;

                    if (clientSocket.Poll(1500 * 1000, SelectMode.SelectRead))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = clientSocket.Receive(buffer);

                        if (bytesRead == 0) break;

                        using (MemoryStream ms = new MemoryStream(buffer))
                        {
                            object primljeniObjekat = formatter.Deserialize(ms);

                            if (!ispitanikPrimljen && primljeniObjekat is Ispitanik ispitanik)
                            {
                                imeKlijenta = $"{ispitanik.Ime} {ispitanik.Prezime}";
                                Console.WriteLine($"[{imeKlijenta}] Podaci o ispitaniku primljeni.");
                                ispitanikPrimljen = true;

                                lock (sviDogadjaji)
                                {
                                    if (!sviDogadjaji.ContainsKey(imeKlijenta))
                                    {
                                        sviDogadjaji[imeKlijenta] = new List<Dogadjaj>();
                                    }
                                }
                            }
                            else if (primljeniObjekat is Dogadjaj dogadjaj)
                            {
                                dogadjajiKlijenta.Add(dogadjaj);

                                Console.WriteLine($"\n[{imeKlijenta}] Primljen događaj: {dogadjaj.PrikazaniSimbol}, " +
                                                  $"Reakcija: {dogadjaj.PritisnutiSimbol}, " +
                                                  $"Vreme: {dogadjaj.ReakcionoVreme:F2}s, " +
                                                  $"Tačnost: {dogadjaj.Tacnost}");

                                lock (sviDogadjaji)
                                {
                                    sviDogadjaji[imeKlijenta].Add(dogadjaj);
                                }

                                // Prikaz statistike u realnom vremenu
                                PrikaziStatistiku(imeKlijenta, dogadjajiKlijenta);
                            }
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greška sa klijentom {clientSocket.RemoteEndPoint}: {ex.Message}");
            }
            finally
            {
                if (clientSocket != null && clientSocket.Connected)
                {
                    clientSocket.Close();
                }
                Console.WriteLine($"Klijent {imeKlijenta} završio vezu.");
            }
        }

        static void PrikaziStatistiku(string imeKlijenta, List<Dogadjaj> dogadjaji)
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

            Console.WriteLine($"[{imeKlijenta}] Statistika: Prosek: {prosecnoVreme:F2}s, " +
                              $"Min: {minimalnoVreme:F2}s, Tacnost: {tacnost:F1}%, " +
                              $"LP: {lazniPozitivi}, LN: {lazniNegativi}");
        }


        static void GenerisiIzvestaj(Dictionary<string, List<Dogadjaj>> sviDogadjaji)
        {
            Console.WriteLine("\nIzveštaj o rezultatima:");

            // Prikaz zaglavlja tabele
            Console.WriteLine("-------------------------------------------------------------");
            Console.WriteLine("{0,-20}{1,-12}{2,-10}{3,-12}{4,-5}{5,-5}",
                "Ispitanik", "Prosek (s)", "Min (s)", "Tačnost (%)", "LP", "LN");
            Console.WriteLine("-------------------------------------------------------------");

            var sortirano = sviDogadjaji
                .Select(kv =>
                {
                    string imeKlijenta = kv.Key;
                    List<Dogadjaj> dogadjaji = kv.Value;

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

                    return new
                    {
                        ImeKlijenta = imeKlijenta,
                        Prosek = prosecnoVreme,
                        Min = minimalnoVreme,
                        Tacnost = tacnost,
                        LP = lazniPozitivi,
                        LN = lazniNegativi
                    };
                })
                .OrderByDescending(x => x.Tacnost) // Sortiramo po tačnosti
                .ThenBy(x => x.Prosek) // Ako je tačnost ista, sortiramo po prosečnom vremenu
                .ToList();

            foreach (var rezultat in sortirano)
            {
                Console.WriteLine("{0,-20}{1,-12:F2}{2,-10:F2}{3,-12:F1}{4,-5}{5,-5}",
                    rezultat.ImeKlijenta, rezultat.Prosek, rezultat.Min, rezultat.Tacnost, rezultat.LP, rezultat.LN);
            }

            Console.WriteLine("-------------------------------------------------------------");
        }

    }
}
