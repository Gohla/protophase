using System;

namespace Protophase.Registry {
    static class MainClass {
        private static Server _server;

        public static void Main(String[] args) {
            Console.CancelKeyPress += CancelKeyPressHandler;
            _server = new Server();
            if (args.Length > 0)
            {
                Console.WriteLine(args[0]);
                Console.WriteLine(args[1]);
            }
            _server.AutoUpdate(1);
            _server.Dispose();
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _server.StopAutoUpdate();
        }
    }
}