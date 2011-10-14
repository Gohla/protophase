using System;
using System.Threading;
using AisTools;
using Protophase.Service;


namespace AisKMLDumper
{
    class KmlDumper
    {
        private static bool quit;
        private static GoogleEarthKMLDumper testKML;
        static void Main(string[] args)
        {
            testKML = new GoogleEarthKMLDumper();
            Console.CancelKeyPress += CancelKeyPressHandler;
            Registry registry = new Registry("tcp://localhost:5555");
            Service aisDataPublisher = null;
            while (aisDataPublisher == null && !quit)
                aisDataPublisher = registry.GetServiceByUID("AisDataPublisher");
            aisDataPublisher.Published += publishedEvent;
            int count = 0;
            while (!quit)
            {
                count++;
                if (count > 10)
                {
                    count = 0;
                    testKML.DumpKMLFile();
                }
                registry.Update();
                Thread.Sleep(100);
            }
        }

        private static void publishedEvent(object obj)
        {
            if (obj is AisData)
                testKML.AddOrUpdateAisTransponder((AisData)obj);
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            quit = true;
        }
    }
}