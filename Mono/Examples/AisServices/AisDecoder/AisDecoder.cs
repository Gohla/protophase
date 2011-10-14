using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using AisTools;
using Protophase.Service;
using Protophase.Shared;

namespace AisReceiverService
{
    class AisReceiverService
    {
        private static Registry registry;
        private static bool quit;
        static void Main(string[] args)
        {
            /* Subscribe to a RAW ais data service
             */
            Console.CancelKeyPress += CancelKeyPressHandler;
            registry = new Registry("tcp://localhost:5555");
            AisDataPublisher aisDataPublisher = new AisDataPublisher();
            if (!registry.Register("AisDataPublisher", aisDataPublisher))
            {
                Console.WriteLine("Error registering service.");
                return;
            }
            Console.WriteLine("Registered service.");
            Service rawDataPublisher = null;
            while (rawDataPublisher == null && !quit)
                rawDataPublisher = registry.GetServiceByUID("RawAisDataPublisher");
            if (!quit)
                rawDataPublisher.Published += RAWAisDataRecieved;

            while (!quit)
            {
                //Thread.Sleep(20);
                registry.Update();
            }
            registry.Unregister("AisDataPublisher");
        }
        /* This method is called each time raw AIS data is recieved, since this method is subscribed to 
         * RawAisDataPublisher
         */
        private static void RAWAisDataRecieved(object obj)
        {
            if (obj is string)
            {
                AisData? ais = AisToolset.DecodeAis((string) obj);
                if (ais != null)
                {
                    Console.Clear();
                    AisToolset.DumpAis((AisData)ais);
                    registry.Publish("AisDataPublisher", ais);
                }
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            quit = true;
            
        }

        [ServiceType("AisDataPublisher"), ServiceVersion("0.1")]
        public class AisDataPublisher
        {
        }
    }
}