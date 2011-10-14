using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protophase.Service;
using AisTools;


namespace AisDataChecksumStatistics
{
    class Program
    {
        private static Registry registry;
        private static bool quit;

        private static int TotalCount = 0;
        private static int OKCount = 0;
        private static int FailCount = 0;
        private static int MultiPartCount = 0;

        static void Main(string[] args)
        {
            registry = new Registry("tcp://localhost:5555");
            Service rawDataPublisher = null;
            while (rawDataPublisher == null && !quit)
                rawDataPublisher = registry.GetServiceByUID("RawAisDataPublisher");
            rawDataPublisher.Published += publishedEvent;
            while (!quit)
            {
                Console.Clear();
                Console.WriteLine("Total messages: " + TotalCount);
                Console.WriteLine("Total messages OK: " + OKCount);
                Console.WriteLine("Total messages Multipart: " + MultiPartCount);
                Console.WriteLine("Total messages Failed: " + FailCount);
                Console.WriteLine("");
                Console.WriteLine("Pecentage OK: " + (((double)OKCount / (double)TotalCount))*100);
                Thread.Sleep(100);
                registry.Update();
            }

        }

        private static void publishedEvent(object obj)
        {
            if (obj is string)
            {
                TotalCount++;
                string fullmessage = (string)obj;
                string[] arr = fullmessage.Split(',');
                if (arr[1] == "2")
                {
                    MultiPartCount++;
                    return;
                }
                if (AisToolset.getChecksum(fullmessage) != fullmessage.Substring(fullmessage.IndexOf('*') + 1))
                {
                    FailCount++;
                }
                OKCount++;
            }
            
        }
    }
}
