using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Diagnostics;
using ClassLibrary;

namespace TCPServer
{
    internal class Server
    {
        static Dictionary<string, List<Dogadjaj>> sviDogadjaji = new Dictionary<string, List<Dogadjaj>>();
        static Dictionary<string, DateTime> vremenaPocetka = new Dictionary<string, DateTime>();
        static int brojKorisnika = 0;
        static int trajanjeEksperimenta = 0;
        static int trenutniKorisnici = 0;
        static readonly object lockObj = new object();

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "🧠 Psihološki Eksperiment - Server";

            ShowServerBanner();

            #region Postavljanje broja korisnika
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("👥 Koliko korisnika će raditi eksperiment? ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            brojKorisnika = int.Parse(Console.ReadLine());
            Console.ResetColor();

            // Pitanje za automatsko pokretanje
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("\n🤖 Želite li automatski pokrenuti klijente? (D/N): ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            string autoStart = Console.ReadLine().ToUpper();
            Console.ResetColor();

            if (autoStart == "D")
            {
                PokreniKlijente(brojKorisnika);
            }

            Console.Clear();
            ShowServerStatus();
            #endregion

            #region Povezivanje
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, 50001));
            serverSocket.Listen(10);
            serverSocket.Blocking = false; // NON-BLOCKING SERVER SOCKET

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Server aktivan na {serverSocket.LocalEndPoint}");
            Console.ResetColor();
            Console.WriteLine($"\n⏳ Čekam povezivanje {brojKorisnika} korisnika...\n");

            List<Socket> klijenti = new List<Socket>();

            // Obezbedjujemo da progress bar bude na posebnom redu!
            int barTop = Console.CursorTop;
            DrawConnectionProgress(0, brojKorisnika);

            while (trenutniKorisnici < brojKorisnika)
            {
                var checkRead = new List<Socket> { serverSocket };
                Socket.Select(checkRead, null, null, 2000 * 1000);
                if (checkRead.Count > 0)
                {
                    Socket clientSocket = serverSocket.Accept();
                    clientSocket.Blocking = false; // <- NON-BLOCKING CLIENT SOCKET

                    klijenti.Add(clientSocket);
                    trenutniKorisnici++;

                    string klijentInfo = clientSocket.RemoteEndPoint.ToString();

                    // Uvek ispisuj novog klijenta u novi red, ispod trenutnog progress bara
                    int oldTop = Console.CursorTop;
                    Console.SetCursorPosition(0, barTop + trenutniKorisnici);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✅ Klijent #{trenutniKorisnici} povezan: {klijentInfo}                  ");
                    Console.ResetColor();

                    // Vraćanje na progress bar red i ažuriranje bara
                    Console.SetCursorPosition(0, barTop);
                    DrawConnectionProgress(trenutniKorisnici, brojKorisnika);

                    // Pomeri kursor ispod poslednjeg reda
                    Console.SetCursorPosition(0, barTop + trenutniKorisnici + 1);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n🎉 Svi klijenti su povezani!\n");
            Console.ResetColor();

            Thread.Sleep(1000);
            Console.Clear();
            ShowExperimentSetup();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("⏱️  Unesite trajanje eksperimenta (u sekundama): ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            trajanjeEksperimenta = int.Parse(Console.ReadLine());
            Console.ResetColor();

            byte[] trajanjeData = BitConverter.GetBytes(trajanjeEksperimenta);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n📤 Slanje instrukcija klijentima...");
            Console.ResetColor();

            foreach (var klijent in klijenti)
            {
                klijent.Send(trajanjeData);
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine($"   ✓ Instrukcije poslate klijentu {klijent.RemoteEndPoint}");
                Console.ResetColor();
                Thread.Sleep(100);
            }
            #endregion

            #region Prikupljanje događaja
            Console.Clear();
            ShowExperimentRunning();

            List<Thread> klijentskeNiti = new List<Thread>();

            foreach (var klijent in klijenti)
            {
                Thread t = new Thread(() => ObradiKlijenta(klijent));
                klijentskeNiti.Add(t);
                t.Start();
            }

            // Prikaz statusa tokom eksperimenta
            Thread statusThread = new Thread(ShowExperimentStatus);
            statusThread.Start();

            // Sačekaj da se svi završe
            foreach (var t in klijentskeNiti)
                t.Join();
            statusThread.Join();
            #endregion

            #region Generisanje izveštaja
            Console.Clear();
            ShowFinalResults();

            GenerisiIzvestaj(sviDogadjaji);
            GenerisiCSV(sviDogadjaji, "izvestaj.csv");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\nPritisnite bilo koji taster za izlaz...");
            Console.ResetColor();
            Console.ReadKey();
            #endregion
        }

        // Ostatak koda ostaje IDENTIČAN

        static void PokreniKlijente(int brojKlijenata)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n🚀 Pokretanje klijenata...\n");
            Console.ResetColor();

            string baseDir = AppContext.BaseDirectory;
            string clientPath = Path.Combine(baseDir, @"..\..\..\TCPClient\bin\Debug\TCPClient.exe");
            clientPath = Path.GetFullPath(clientPath);

            if (!File.Exists(clientPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ TCPClient.exe nije pronađen u:\n   {clientPath}");
                Console.WriteLine("   Molimo kompajlirajte TCPClient projekat prvo!");
                Console.ResetColor();
                Console.WriteLine("\nPokrenite klijente ručno...");
                Thread.Sleep(3000);
                return;
            }

            for (int i = 0; i < brojKlijenata; i++)
            {
                try
                {
                    Process klijentProces = new Process();
                    klijentProces.StartInfo.FileName = clientPath;
                    klijentProces.StartInfo.UseShellExecute = true;
                    klijentProces.StartInfo.CreateNoWindow = false;
                    klijentProces.Start();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   ✓ Pokrenut klijent #{i + 1}");
                    Console.ResetColor();

                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"   ❌ Greška pri pokretanju klijenta #{i + 1}: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n⏳ Sačekajte da se klijenti povežu...");
            Console.ResetColor();
            Thread.Sleep(2000);
        }

        static void ObradiKlijenta(Socket clientSocket)
        {
            BinaryFormatter formatter = new BinaryFormatter();
            string imeKlijenta = "";
            List<Dogadjaj> dogadjajiKlijenta = new List<Dogadjaj>();
            DateTime vremePocetka = DateTime.Now;

            try
            {
                bool ispitanikPrimljen = false;

                while (true)
                {
                    if (clientSocket == null || !clientSocket.Connected) break;

                    var checkRead = new List<Socket> { clientSocket };
                    Socket.Select(checkRead, null, null, 1500 * 1000);

                    if (checkRead.Count > 0)
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
                                vremePocetka = DateTime.Now;

                                lock (lockObj)
                                {
                                    Console.ForegroundColor = ConsoleColor.Cyan;
                                    Console.WriteLine($"\n📋 [{imeKlijenta}] Registrovan - ID: {ispitanik.ID}, Starost: {ispitanik.Starost}");
                                    Console.ResetColor();

                                    vremenaPocetka[imeKlijenta] = vremePocetka;
                                    ispitanikPrimljen = true;

                                    if (!sviDogadjaji.ContainsKey(imeKlijenta))
                                    {
                                        sviDogadjaji[imeKlijenta] = new List<Dogadjaj>();
                                    }
                                }
                            }
                            else if (primljeniObjekat is Dogadjaj dogadjaj)
                            {
                                dogadjajiKlijenta.Add(dogadjaj);

                                lock (lockObj)
                                {
                                    sviDogadjaji[imeKlijenta].Add(dogadjaj);

                                    // Real-time prikaz sa bojama
                                    Console.ForegroundColor = ConsoleColor.DarkGray;
                                    Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
                                    Console.ForegroundColor = ConsoleColor.White;
                                    Console.Write($"{imeKlijenta}: ");

                                    if (dogadjaj.Tacnost)
                                    {
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.Write("✅ TAČNO ");
                                    }
                                    else
                                    {
                                        Console.ForegroundColor = ConsoleColor.Red;
                                        Console.Write("❌ NETAČNO ");
                                    }

                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"({dogadjaj.ReakcionoVreme * 1000:F0}ms)");
                                    Console.ResetColor();
                                }
                            }
                        }
                    }
                }
            }
            catch (SocketException ex)
            {
                lock (lockObj)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n❌ Greška sa klijentom {imeKlijenta}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            finally
            {
                DateTime vremeZavrsetka = DateTime.Now;
                lock (lockObj)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n🏁 [{imeKlijenta}] Završio eksperiment");
                    Console.ResetColor();
                }

                if (clientSocket != null && clientSocket.Connected)
                {
                    clientSocket.Close();
                }
            }
        }

        // --- SEKCIJA ZA ISPIS I FORMATIRANJE ---

        static void ShowServerBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╔══════════════════════════════════════════════════════════════╗
    ║                                                              ║
    ║              🧠 PSIHOLOŠKI EKSPERIMENT SERVER 🧠             ║
    ║                                                              ║
    ║                    Kontrolni Centar                          ║
    ║                                                              ║
    ╚══════════════════════════════════════════════════════════════╝
            ");
            Console.ResetColor();
        }

        static void ShowServerStatus()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n    ╔════════════════════════════╗");
            Console.WriteLine("    ║     SERVER STATUS: ON      ║");
            Console.WriteLine("    ╚════════════════════════════╝");
            Console.ResetColor();
        }

        static void ShowExperimentSetup()
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n    ╔════════════════════════════╗");
            Console.WriteLine("    ║   PODEŠAVANJE EKSPERIMENTA ║");
            Console.WriteLine("    ╚════════════════════════════╝\n");
            Console.ResetColor();
        }

        static void ShowExperimentRunning()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n    ╔════════════════════════════╗");
            Console.WriteLine("    ║   EKSPERIMENT U TOKU 🔴    ║");
            Console.WriteLine("    ╚════════════════════════════╝\n");
            Console.ResetColor();
        }

        static void ShowFinalResults()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n    ╔════════════════════════════╗");
            Console.WriteLine("    ║    📊 FINALNI REZULTATI    ║");
            Console.WriteLine("    ╚════════════════════════════╝\n");
            Console.ResetColor();
        }

        // Ispravljeni progress bar – UVEK briše liniju
        static void DrawConnectionProgress(int current, int total)
        {
            Console.Write("    Progres: [");

            int barWidth = 30;
            int filled = (int)(barWidth * current / (double)total);

            // Zeleni ispunjeni deo
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', filled));

            // Tamnosivi prazni deo
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', barWidth - filled));

            Console.ResetColor();
            Console.Write($"] {current}/{total}".PadRight(20)); // dovoljno dugačak padding

            Console.Write("    "); // još malo praznog za svaki slučaj
        }


        static void ShowExperimentStatus()
        {
            while (true)
            {
                lock (lockObj)
                {
                    int aktivniKlijenti = sviDogadjaji.Count(kv => kv.Value.Count > 0);
                    if (aktivniKlijenti == brojKorisnika) break;
                }
                Thread.Sleep(1000);
            }
        }

        static void GenerisiIzvestaj(Dictionary<string, List<Dogadjaj>> sviDogadjaji)
        {
            Console.WriteLine("\n📊 TABELA REZULTATA:");
            Console.WriteLine(new string('═', 80));

            // Pravilno zaglavlje sa jedinicama!
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("{0,-20}{1,12}{2,12}{3,12}{4,8}{5,8}{6,8}",
                "Ispitanik", "Prosek (ms)", "Min (ms)", "Tačnost (%)", "LP", "LN", "Rang");
            Console.WriteLine(new string('─', 80));
            Console.ResetColor();

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
                .OrderByDescending(x => x.Tacnost)
                .ThenBy(x => x.Prosek)
                .ToList();

            int rang = 1;
            foreach (var rezultat in sortirano)
            {
                if (rang == 1)
                    Console.ForegroundColor = ConsoleColor.Yellow;
                else if (rang == 2)
                    Console.ForegroundColor = ConsoleColor.Gray;
                else if (rang == 3)
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                else
                    Console.ForegroundColor = ConsoleColor.White;

                string medalja = rang == 1 ? "🥇" : rang == 2 ? "🥈" : rang == 3 ? "🥉" : "";

                Console.WriteLine("{0,-20}{1,12:F2}{2,12:F2}{3,12:F1}{4,8}{5,8}{6,8}",
                    rezultat.ImeKlijenta,
                    rezultat.Prosek * 1000,
                    rezultat.Min * 1000,
                    rezultat.Tacnost,
                    rezultat.LP,
                    rezultat.LN,
                    medalja);

                rang++;
                Console.ResetColor();
            }

            Console.WriteLine(new string('═', 80));

            // Statistički pregled
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("\n📈 STATISTIČKI PREGLED:");
            Console.ResetColor();

            var prosecnaTacnost = sortirano.Average(x => x.Tacnost);
            var prosecnoVremeReakcije = sortirano.Average(x => x.Prosek) * 1000;
            var najbrziIspitanik = sortirano.OrderBy(x => x.Prosek).First();
            var najtacnijiIspitanik = sortirano.First();

            Console.WriteLine($"   • Prosečna tačnost grupe: {prosecnaTacnost:F1}%");
            Console.WriteLine($"   • Prosečno vreme reakcije: {prosecnoVremeReakcije:F0}ms");
            Console.WriteLine($"   • Najtačniji ispitanik: {najtacnijiIspitanik.ImeKlijenta} ({najtacnijiIspitanik.Tacnost:F1}%)");
            Console.WriteLine($"   • Najbrži ispitanik: {najbrziIspitanik.ImeKlijenta} ({najtacnijiIspitanik.Prosek * 1000:F0}ms)");
        }

        static void GenerisiCSV(Dictionary<string, List<Dogadjaj>> sviDogadjaji, string putanja)
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(putanja))
                {
                    sw.WriteLine("Ime i Prezime,Prosecno Vreme (ms),Minimalno Vreme (ms),Tacnost (%),Lazni Pozitivi,Lazni Negativi,Ukupno Pokusaja");

                    foreach (var par in sviDogadjaji)
                    {
                        string imeKlijenta = par.Key;
                        List<Dogadjaj> dogadjaji = par.Value;

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

                        sw.WriteLine($"{imeKlijenta},{prosecnoVreme * 1000:F2},{minimalnoVreme * 1000:F2},{tacnost:F1},{lazniPozitivi},{lazniNegativi},{dogadjaji.Count}");
                    }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n💾 CSV izveštaj uspešno generisan: {Path.GetFullPath(putanja)}");
                Console.ResetColor();

                // Vizuelni prikaz da je fajl kreiran
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.WriteLine(@"
    ╔══════════════════════════════════╗
    ║     📄 FAJL USPEŠNO KREIRAN      ║
    ╚══════════════════════════════════╝
                ");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ Greška prilikom generisanja CSV fajla: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}

