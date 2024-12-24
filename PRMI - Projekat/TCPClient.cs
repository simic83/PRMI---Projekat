using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Telepsihologija
{
    class Program
    {
        static void Main(string[] args)
        {
            // KT1 - Pokretanje servera i klijenta
            Thread serverThread = new Thread(StartServer);
            serverThread.Start();

            Thread clientThread = new Thread(StartClient);
            clientThread.Start();
        }

        // KT1 - Zadaci 1 i 2: Implementacija servera i klijenta
        static void StartServer()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 15000);
            server.Start();

            Console.WriteLine("Server je pokrenut na portu 15000...");

            while (true)
            {
                // Prihvatanje konekcije klijenta
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Klijent povezan.");

                // Obrada klijenta u posebnoj niti
                Thread clientHandler = new Thread(() => HandleClient(client));
                clientHandler.Start();
            }
        }

        // KT1 - Zadatak 3: Obrada podataka klijenta na serveru
        static void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string clientData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Console.WriteLine($"Podaci primljeni od klijenta: {clientData}");

            // Simulacija obrade podataka i generisanje rezultata
            string[] dataParts = clientData.Split(',');
            string ime = dataParts[0].Split(':')[1].Trim();
            string prezime = dataParts[1].Split(':')[1].Trim();
            string id = dataParts[2].Split(':')[1].Trim();

            string response = $"Pozdrav, {ime} {prezime} (ID: {id}). Podaci su uspešno evidentirani!";
            buffer = Encoding.UTF8.GetBytes(response);
            stream.Write(buffer, 0, buffer.Length);

            client.Close();
        }

        // KT1 - Zadatak 4: Klijent šalje podatke i prima odgovor
        static void StartClient()
        {
            Thread.Sleep(1000); // Kratka pauza da server startuje

            TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", 15000);

            NetworkStream stream = client.GetStream();
            string dataToSend = "Ime: Marko, Prezime: Marković, ID: 1234";

            byte[] buffer = Encoding.UTF8.GetBytes(dataToSend);
            stream.Write(buffer, 0, buffer.Length);

            buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string serverResponse = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            Console.WriteLine($"Odgovor servera: {serverResponse}");

            client.Close();
        }

        // KT1 - Zadatak 5: Blok dijagram (opis u komentarima)
        /*
        Blok dijagram sistema:
        - Server:
          * TCP Listener na portu 15000
          * Prihvata klijente i obrađuje podatke u zasebnim nitima
        - Klijent:
          * Povezuje se na server preko TCP-a
          * Šalje podatke (ime, prezime, ID) i prima odgovor
        */
    }
}
