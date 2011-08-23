using System;

namespace Protophase.Registry {
    class MainClass {
        public static void Main(string[] args) {
            Server server = new Server("tcp://*:5555");
            server.Start();
        }
    }
}