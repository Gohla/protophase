using System;
using System.Text;
using Protophase.Service;
using Protophase.Shared;
using ZMQ;

namespace TestServiceClient {
    class MainClass {
        public static void Main(string[] args) {
            ServiceInfo info = new ServiceInfo("Troll", "TrollServ", "1.0", "tcp://localhost:6666");
            Registry registry = new Registry("tcp://localhost:5555");

            Console.WriteLine("Registering service: " + info);
            if(!registry.Register(info)) Console.WriteLine("Registration failed");

            Console.WriteLine("Search for service: " + info.UID);
            ServiceInfo serviceInfo = registry.FindByUID(info.UID);
            if(serviceInfo != null) Console.WriteLine("Found service: " + serviceInfo);
            else Console.WriteLine("Service not found");

            Console.WriteLine("Unregistering service: " + info.UID);
            if(!registry.Unregister(info.UID)) Console.WriteLine("Unregistratation failed");
        }
    }
}

