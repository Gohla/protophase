using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using AisTools;
using Protophase.Service;
using Protophase.Shared;

namespace AisReceiverService
{
    internal class AisReceiverService
    {
        private static AisDataPublisher p;
        private static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            p = new AisDataPublisher();
            p.Start();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            p.quit = true;
        }
    }


    [ServiceType("AisDataPublisher"), ServiceVersion("0.1")]
    public class AisDataPublisher
    {
        [Publisher]
        public event PublishedDelegate AisData;


        private Registry registry;
        public bool quit;

        public void Start()
        {
            registry = new Registry();
            if (!registry.Register(this))
            {
                Console.WriteLine("Error registering service.");
                return;
            }
            Console.WriteLine("Registered service.");
            Service rawDataPublisher = null;
            while (rawDataPublisher == null && !quit)
                rawDataPublisher = registry.GetServiceByType("RawAisDataPublisher");
            if (!quit)
                rawDataPublisher.Published += RAWAisDataRecieved;
            while (!quit)
                registry.Update();
            registry.Unregister(this);
        }

        /* This method is called each time raw AIS data is recieved, since this method is subscribed to 
             * RawAisDataPublisher
             */

        private void RAWAisDataRecieved(object obj)
        {
            if (obj is string)
            {
                AisData? ais = AisToolset.DecodeAis((string) obj);
                if (ais != null)
                {
                    Console.Clear();
                    AisToolset.DumpAis((AisData) ais);
                    if (AisData != null)
                        AisData(ais);
                }
            }
        }
    }
}