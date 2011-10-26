using System;
using System.Threading;
using Protophase.Service;
using System.IO;
using Protophase.Shared;

namespace RawAisServiceTestdata
{
    class RawAisPublisherTestData
    {
        private static AisDataPublisherTestData p;
        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressHandler;
            p = new AisDataPublisherTestData();
            p.Start();
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            p.quit = true;
        }

    }
    [ServiceType("RawAisDataPublisher"), ServiceVersion("0.1")]
    public class AisDataPublisherTestData
    {
        public bool quit;
        private Registry registry;

        [Publisher]
        public event PublishedDelegate RawAisData;

        public void Start()
        {
            registry = new Registry();
            if (!registry.Register(this))
            {
                Console.WriteLine("Error registering service.");
                return;
            }
            Console.WriteLine("Registered service.");
            //try
            {
                string[] dummydata = File.ReadAllLines(@"..\..\data.txt");

                while (!quit)
                {
                    foreach (var data in dummydata)
                    {
                        if (quit)
                            break;
                        Console.WriteLine("Raw data: " + data);
                        if (RawAisData != null)
                            RawAisData(data);
                        
                        Thread.Sleep((int)(new Random().NextDouble() * 1000));
                        registry.Update();
                    }
                }
                //Shut down after finishing & clean up
                registry.Unregister(this);
            }
        /*
            catch (Exception e)
            {
                Console.WriteLine(e.Message + e.StackTrace);
                Console.WriteLine("Press enter");
                Console.ReadLine();
            }     
         */

        }

    }
}
