using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace SimpleRPCClient {
    static class MainClass {
        private static Registry _registry;
        private static Service _testServer;

        private static void Main(string[] args) {
            Console.CancelKeyPress += CancelKeyPressHandler;

            using(_registry = new Registry("tcp://localhost:5555")) {
                while(_testServer == null) _testServer = _registry.GetServiceByUID("TestServer");
                _registry.Idle += SendRPC;
                _registry.AutoUpdate();
            }
        }

        private static void SendRPC() {
            String param = "TEST";
            String returnVal = _testServer.Call<String>("TestMethod", param);
            Console.WriteLine("Send RPC TestMethod, param: " + param + " return: " + returnVal);
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _registry.StopAutoUpdate();
        }
    }
}
