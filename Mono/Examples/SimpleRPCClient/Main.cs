using System;
using Protophase.Service;
using Protophase.Shared;
using Protophase.Examples;

namespace SimpleRPCClient {
    class Application : ExampleApplication {
        private static Service _testServer;

        private static void Main(string[] args) {
            using(Application app = new Application()) {
                app.Start();
            }
        }

        protected override void Init() {
            while(_testServer == null) _testServer = _registry.GetServiceByUID("TestServer");
        }

        protected override void Idle() {
            String param = "TEST";
            String returnVal = _testServer.Call<String>("TestMethod", param);
            Console.WriteLine("Send RPC TestMethod, param: " + param + " return: " + returnVal);
        }
    }
}
