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
        private static readonly string[] LoadingChars = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private static int loadingIndex = 0;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "🧠 Psihološki Eksperiment - Klijent";

            ShowWelcomeScreen();

            #region Povezivanje
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 50001);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n⏳ Povezivanje na server");

            try
            {
                var connectTask = System.Threading.Tasks.Task.Run(() => clientSocket.Connect(serverEP));
                while (!connectTask.IsCompleted)
                {
                    Console.Write($"\r⏳ Povezivanje na server {LoadingChars[loadingIndex++ % LoadingChars.Length]}");
                    Thread.Sleep(100);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\r✅ Uspešno povezano na server!                    ");
                Console.ResetColor();

                Thread.Sleep(500);
            }
            catch (SocketException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\r❌ Greška prilikom povezivanja: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("\nPritisnite bilo koji taster za izlaz...");
                Console.ReadKey();
                return;
            }
            #endregion

            #region Login ispitanika
            Console.Clear();
            ShowHeader("REGISTRACIJA ISPITANIKA");

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("📋 Molimo unesite podatke o ispitaniku:\n");
            Console.ResetColor();

            string ime = GetInput("👤 Ime", ConsoleColor.White);
            string prezime = GetInput("👤 Prezime", ConsoleColor.White);
            string id = GetInput("🆔 ID", ConsoleColor.White);
            int starost = GetIntInput("🎂 Starost", ConsoleColor.White);

            Ispitanik ispitanik = new Ispitanik
            {
                Ime = ime,
                Prezime = prezime,
                ID = id,
                Starost = starost
            };

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n📤 Slanje podataka serveru");

            BinaryFormatter formatter = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                formatter.Serialize(ms, ispitanik);
                byte[] data = ms.ToArray();

                for (int i = 0; i < 5; i++)
                {
                    Console.Write(".");
                    Thread.Sleep(200);
                }

                clientSocket.Send(data);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ✅ Uspešno!");
                Console.ResetColor();
            }
            #endregion

            #region Prijem trajanja eksperimenta (Socket.Select)
            byte[] trajanjeData = new byte[4];
            bool trajanjePrimljeno = false;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n⏱️  Čekanje na instrukcije");

            while (!trajanjePrimljeno)
            {
                var checkRead = new System.Collections.Generic.List<Socket> { clientSocket };
                Socket.Select(checkRead, null, null, 1000000); // timeout 1s

                if (checkRead.Count > 0)
                {
                    int received = clientSocket.Receive(trajanjeData);
                    if (received > 0)
                    {
                        int trajanjeEksperimenta = BitConverter.ToInt32(trajanjeData, 0);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"\r⏱️  Trajanje eksperimenta: {trajanjeEksperimenta} sekundi ✅");
                        Console.ResetColor();

                        Thread.Sleep(1000);

                        // ODMAH pokreće eksperiment, bez čekanja na taster!
                        SimulacijaEksperimenta(clientSocket, formatter, trajanjeEksperimenta);
                        trajanjePrimljeno = true;
                    }
                }
                else
                {
                    Console.Write($"\r⏱️  Čekanje na instrukcije {LoadingChars[loadingIndex++ % LoadingChars.Length]}");
                }
            }
            #endregion

            #region Zatvaranje
            Console.Clear();
            ShowHeader("EKSPERIMENT ZAVRŠEN");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Hvala na učešću!\n");
            Console.ResetColor();

            Console.WriteLine("📊 Vaši rezultati su uspešno sačuvani.");
            Console.WriteLine("📧 Rezultate ćete dobiti na email adresu.\n");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Pritisnite bilo koji taster za izlaz...");
            Console.ResetColor();
            Console.ReadKey();

            clientSocket.Close();
            #endregion
        }

        static void SimulacijaEksperimenta(Socket clientSocket, BinaryFormatter formatter, int trajanjeEksperimenta)
        {
            Console.Clear();
            ShowHeader("EKSPERIMENT U TOKU");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("⚡ INSTRUKCIJE:");
            Console.ResetColor();
            Console.WriteLine("• Pritisnite SPACE kada vidite simbol 'O'");
            Console.WriteLine("• Ignorišite simbol 'X'\n");

            Random random = new Random();
            Stopwatch ukupanTajmer = Stopwatch.StartNew();
            int brojPokusaja = 0;
            int tacniOdgovori = 0;

            while (ukupanTajmer.Elapsed.TotalSeconds < trajanjeEksperimenta)
            {
                brojPokusaja++;
                Console.Clear();

                double progress = (ukupanTajmer.Elapsed.TotalSeconds / trajanjeEksperimenta) * 100;
                DrawProgressBar(progress, trajanjeEksperimenta - (int)ukupanTajmer.Elapsed.TotalSeconds);

                Console.WriteLine($"\n📊 Pokušaj: {brojPokusaja} | ✅ Tačni: {tacniOdgovori} | 📈 Tačnost: {(brojPokusaja > 0 ? (tacniOdgovori * 100.0 / brojPokusaja) : 0):F1}%\n");

                string simbol = random.Next(2) == 0 ? "X" : "O";
                Console.ForegroundColor = simbol == "X" ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine("\n\n");
                DrawLargeSymbol(simbol);
                Console.ResetColor();

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

                if (tacno) tacniOdgovori++;

                Console.ForegroundColor = tacno ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine($"\n{(tacno ? "✅ TAČNO" : "❌ NETAČNO")} - Reakciono vreme: {stopwatch.Elapsed.TotalMilliseconds:F0}ms");
                Console.ResetColor();

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
                }

                Thread.Sleep(500);
            }
        }

        static void ShowWelcomeScreen()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╔══════════════════════════════════════════════════════════╗
    ║                                                          ║
    ║           🧠 PSIHOLOŠKI EKSPERIMENT 🧠                   ║
    ║                                                          ║
    ║              Test Reakcionog Vremena                     ║
    ║                                                          ║
    ╚══════════════════════════════════════════════════════════╝
            ");
            Console.ResetColor();

            Console.WriteLine("    Dobrodošli u eksperiment testiranja reakcionog vremena!");
            Console.WriteLine("    Cilj: Merenje brzine i tačnosti vaših reakcija.\n");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("    Pritisnite bilo koji taster za početak...");
            Console.ResetColor();
            Console.ReadKey();
        }

        static void ShowHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n    ╔══════════════════════════════════════════╗");
            Console.WriteLine($"    ║   {title.PadRight(38)}    ║");
            Console.WriteLine($"    ╚══════════════════════════════════════════╝\n");
            Console.ResetColor();
        }

        static string GetInput(string prompt, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write($"    {prompt}: ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            string input = Console.ReadLine();
            Console.ResetColor();
            return input;
        }

        static int GetIntInput(string prompt, ConsoleColor color)
        {
            while (true)
            {
                Console.ForegroundColor = color;
                Console.Write($"    {prompt}: ");
                Console.ForegroundColor = ConsoleColor.Yellow;
                string input = Console.ReadLine();
                Console.ResetColor();

                if (int.TryParse(input, out int result))
                    return result;

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("    ❌ Molimo unesite validan broj!");
                Console.ResetColor();
            }
        }

        static void DrawProgressBar(double progress, int remainingSeconds)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("    Napredak: [");

            int barWidth = 40;
            int filled = (int)(barWidth * progress / 100);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', filled));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', barWidth - filled));

            Console.Write($"] {progress:F1}% | ⏱️  Preostalo: {remainingSeconds}s");
            Console.ResetColor();
        }

        static void DrawLargeSymbol(string symbol)
        {
            Console.WriteLine("\n\n\n\n\n");

            if (symbol == "X")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("                    ╔════════════════════╗");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║        ❌          ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║         X          ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║   NE PRITISKAJ!    ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ╚════════════════════╝");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("                    ╔════════════════════╗");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║        ⭕          ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║         O          ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ║  PRITISNI SPACE!   ║");
                Console.WriteLine("                    ║                    ║");
                Console.WriteLine("                    ╚════════════════════╝");
            }

            Console.ResetColor();
            Console.WriteLine("\n\n\n");
        }
    }
}
