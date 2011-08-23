using System;
using System.Text;
using Protophase.Service;
using Protophase.Shared;
using ZMQ;

namespace TestServiceClient {
    class MainClass {
        public static void Main(string[] args) {
            Registry registry = new Registry("tcp://localhost:5555");
            Console.WriteLine("Registering service");
            registry.Register(new ServiceInfo("Troll", "TrollServ", "1.0", "tcp://localhost:6666"));
        }
    }
}

