using System;
using Protophase.Shared;
using ZMQ;

namespace Protophase.Registry {
    static class MainClass {
        private static Server _server;

        public static void Main(String[] args) {
            Console.CancelKeyPress += CancelKeyPressHandler;
            if (args.Length == 4)
            {
                _server = new Server(new Address(Transport.TCP, "*", (ushort)Int32.Parse(args[0])), new Address(Transport.TCP, "*", (ushort)Int32.Parse(args[1])));
                _server.AddToServerPool(
                        new Address(Transport.TCP, "localhost", (ushort)Int32.Parse(args[2])),
                        new Address(Transport.TCP, "localhost", (ushort)Int32.Parse(args[3])),
                        new Address(Transport.TCP, "localhost", (ushort)Int32.Parse(args[0])),
                        new Address(Transport.TCP, "localhost", (ushort)Int32.Parse(args[1]))
                    );
            }
            else
            {
                _server = new Server(new Address(Transport.TCP, "*", 5555), new Address(Transport.TCP, "*", 5556));
            }
            _server.AutoUpdate(500);
            _server.Dispose();
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _server.StopAutoUpdate();
        }
    }
}