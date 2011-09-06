using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace TestServiceServer {
    static class MainClass {
        static bool quit = false;

        public static void Main(string[] args) {
            // Get console quit key press events.
            Console.CancelKeyPress += CancelKeyPressHandler;

            // Create registry client for registry at address tcp://localhost:5555.
            using(Registry registry = new Registry("tcp://localhost:5555")) {
                // Register our hello world responder object with the registry.
                HelloWorldResponder responder = new HelloWorldResponder();
                if(registry.Register("HelloWorldResponder", responder))
                    Console.WriteLine("Registered HelloWorldResponder");
                else {
                    Console.WriteLine("Failed to register HelloWorldResponder");
                    return;
                }

                // Program loop
                while(!quit) {
                    // Emit an event in the service.
                    responder.SomethingHappened();

                    // Receive RPC calls
                    // TODO: Optionally let the library take care of this.
                    registry.Receive();

                    Thread.Sleep(10);
                }
            }
        }

        // Handler for console cancel key presses.
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            quit = true;
        }
    }

    // Simple hello world responder.
    [ServiceType("HelloWorld"), ServiceVersion("0.1")]
    public class HelloWorldResponder {
        [Publisher]
        public event PublishedEvent Hello;
        [Publisher]
        public event PublishedEvent World;

        [RPC]
        public String HelloWorld() {
            Console.WriteLine("Hello");
            return "World";
        }

        [RPC]
        public String ReturnParam(int par1, float par2, String par3) {
            return par1 + " " + par2 + " " + par3;
        }

        public void SomethingHappened() { 
            if(Hello != null) Hello("Hello");
            if(World != null) World("world!");
        }
    }
}

