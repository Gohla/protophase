using System;
using Protophase.Service;
using Protophase.Shared;
using Protophase.Examples;

namespace SimpleRPCServer {
    class Application : ExampleApplication {
        static void Main(string[] args) {
            using(Application app = new Application()) {
                app.Start();
            }
        }

        protected override void Init() {
            TestServer testServer = new TestServer();
            _registry.Register(testServer);
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
