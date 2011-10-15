using System;
using Protophase.Service;
using Protophase.Shared;
using Protophase.Examples;

namespace SimpleRPCClient {
    class Application : ExampleApplication {
        private static Service _testServer;

        private static void Main(string[] args) {
            using(Application app = new Application()) {
                app.Start(500);
            }
        }

        protected override void Init() {
            while(_testServer == null) _testServer = _registry.GetServiceByType("TestServer");
        }

        protected override void Idle() {
            try {
                String param = "TEST";
                String returnVal = _testServer.Call<String>("TestMethod", param);
                Console.WriteLine("Send RPC TestMethod, param: " + param + " return: " + returnVal);
            }
            catch(System.Exception e) {
            	Console.WriteLine("Failed to send RPC TestMethod: " + e.Message);
            }
        }
    }
}
