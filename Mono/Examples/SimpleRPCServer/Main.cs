using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace SimpleRPCServer {
    static class MainClass {
        private static Registry _registry;

        private static void Main(string[] args) {
            Console.CancelKeyPress += CancelKeyPressHandler;

            using(_registry = new Registry("tcp://localhost:5555")) {
                TestServer testServer = new TestServer();
                _registry.Register("TestServer", testServer);
                _registry.AutoUpdate();
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _registry.StopAutoUpdate();
        }
    }

    [ServiceType("TestServer"), ServiceVersion("0.1")]
    public class TestServer {
        [RPC]
        public String TestMethod(String param) {
            Console.WriteLine("Received RPC TestMethod, param: " + param);
            return param;
        }
    }
}
