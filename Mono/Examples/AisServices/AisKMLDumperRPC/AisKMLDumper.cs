using System;
using System.Threading;
using AisTools;
using Protophase.Service;


namespace AisKMLDumperRPC
{
    class KmlDumperRPC
    {
        private static bool quit;
        static void Main(string[] args)
        {
            GoogleEarthKMLDumper testKML = new GoogleEarthKMLDumper();
            Console.CancelKeyPress += CancelKeyPressHandler;
            Registry registry = new Registry("tcp://localhost:5555");

            //Register this KMLDumper
            if (registry.Register("KMLDumperInstance", testKML))
                Console.WriteLine("Registered KMLDumperInstance");
            else
            {
                Console.WriteLine("Failed to register KMLDumperInstance");
                return;
            }
            int i = 0;
            while (!quit)
            {
                i++;
                registry.Update();
                
                Thread.Sleep(10);
                //Dump new data every second
                if (i % 100 == 0)
                {
                    testKML.DumpKMLFile();
                    i = 0;
                }
            }
            if (registry.Unregister("KMLDumperInstance"))
                Console.WriteLine("Unregistered KMLDumperInstance");
            else
                Console.WriteLine("Failed to unregister KMLDumperInstance");
        }
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            quit = true;
        }
    }
}