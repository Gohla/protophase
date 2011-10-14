using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using AisTools;
using Protophase.Service;

namespace AisReceiverServiceRPC
{
    class AisReceiverServiceRPC
    {
        private const bool TESTMODE = true;

        static void Main(string[] args)
        {
            Registry registry = new Registry("tcp://localhost:5555");

            // Get KMLDumper service
            Service KMLDumperInstance = registry.GetService("KMLDumperInstance"); //only UID?
            if (KMLDumperInstance == null)
            {
                Console.WriteLine("Failed because a KMLDumperInstance was not returned by the registry. Press enter");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("Found KMLDumper: " + KMLDumperInstance);
            try
            {

                if (TESTMODE)
                {
                    //In testmode earlier captured data is used as input since the AIS server at mr Gross's house occasionally goes offline.
                    string[] dummydata = File.ReadAllLines(@"..\..\data.txt");
                    foreach (var data in dummydata)
                    {
                        Console.Clear();
                        AisData? ais = AisToolset.DecodeAis(data);
                        if (ais != null)
                        {
                            AisToolset.DumpAis((AisData)ais);
                            Console.WriteLine(KMLDumperInstance.Call<String>("AddOrUpdateAisTransponder", (AisData)ais));
                        }
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    TcpClient tcpclient = new TcpClient();
                    tcpclient.Connect("82.210.120.176", 2000);
                    StreamReader sr = new StreamReader(tcpclient.GetStream());
                    string data;
                    while ((data = sr.ReadLine()) != null)
                    {
                        AisData? ais = AisToolset.DecodeAis(data);
                        Console.Clear();
                        if (ais != null)
                        {
                            AisToolset.DumpAis((AisData)ais);
                            Console.WriteLine(KMLDumperInstance.Call<String>("AddOrUpdateAisTransponder", (AisData)ais));
                        }
                    }
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