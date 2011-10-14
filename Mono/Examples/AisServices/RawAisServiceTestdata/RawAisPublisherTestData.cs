using System;
using System.Threading;
using Protophase.Service;
using System.IO;
using Protophase.Shared;

namespace RawAisServiceTestdata
{
    class RawAisPublisherTestData
    {
        private static bool quit;
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
                string[] dummydata = File.ReadAllLines(@"..\..\data.txt");

                while (!quit) 
                {
                    foreach (var data in dummydata)
                    {
                        if (quit)
                            break;
                        Console.WriteLine("Raw data: " + data);
                        registry.Publish("RawAisDataPublisher", data);
                        Thread.Sleep((int) (new Random().NextDouble()*1000));
                    }
                }
                //Shut down after finishing & clean up
                registry.Unregister("RawAisDataPublisher");
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
            quit = true;
            e.Cancel = true;
        }

    }
    [ServiceType("RawAisDataPublisher"), ServiceVersion("0.1")]
    public class AisDataPublisher
    {
    }
}
