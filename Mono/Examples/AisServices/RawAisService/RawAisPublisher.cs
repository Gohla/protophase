using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Protophase.Service;
using System.IO;
using Protophase.Shared;

namespace RawAisService
{
    class RawAisPublisher
    {
        private static Registry registry;
        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressHandler;
            registry = new Registry("tcp://localhost:5555");
            AisDataPublisher aisDataPublisher = new AisDataPublisher();
            if (!registry.Register("RawAisDataPublisher", aisDataPublisher))
            {
                Console.WriteLine("Error registering service.");
                return;
            }
            Console.WriteLine("Registered service.");
            try
            {
                TcpClient tcpclient = new TcpClient();
                tcpclient.Connect(args[1], Int32.Parse(args[2]));
                StreamReader sr = new StreamReader(tcpclient.GetStream());
                string data;
                while ((data = sr.ReadLine()) != null)
                {
                    Console.WriteLine("Raw data: " + data);
                    registry.Publish("RawAisDataPublisher", data);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + e.StackTrace);
                Console.WriteLine("Press enter");
                Console.ReadLine();
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            registry.Unregister("RawAisDataPublisher");
        }

        [ServiceType("RawAisDataPublisher"), ServiceVersion("0.1")]
        public class AisDataPublisher
        {
        }
    }
}
