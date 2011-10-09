using System;

namespace Protophase.Registry {
    static class MainClass {
        private static Server _server;

        public static void Main(String[] args) {
            Console.CancelKeyPress += CancelKeyPressHandler;

            _server = new Server("tcp://*:5555", "tcp://*:5556");
            _server.AutoUpdate();
            _server.Dispose();
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _server.StopAutoUpdate();
        }
    }
}