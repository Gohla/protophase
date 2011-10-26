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
            new AisDataPublisher().Start(args);
        }

        [ServiceType("RawAisDataPublisher"), ServiceVersion("0.1")]
        public class AisDataPublisher
        {
            [Publisher]
            public event PublishedDelegate RawAisData;

            public void Start(string[] args)
            {
                registry = new Registry();
                if (!registry.Register(this))
                {
                    Console.WriteLine("Error registering service.");
                    return;
                }
                Console.WriteLine("Registered service.");
                try
                {
                    TcpClient tcpclient = new TcpClient();
                    if (args.Count() != 2)
                        throw new Exception("Argument must contain a publishing ip and port. call with: 127.0.0.1 12345");
                    tcpclient.Connect(args[0], Int32.Parse(args[1]));
                    StreamReader sr = new StreamReader(tcpclient.GetStream());
                    string data;
                    while ((data = sr.ReadLine()) != null)
                    {
                        Console.WriteLine("Raw data: " + data);
                        if (RawAisData != null)
                            RawAisData(data);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + e.StackTrace);
                    Console.WriteLine("Press enter");
                    Console.ReadLine();
                }
            }
        }
    }
}
