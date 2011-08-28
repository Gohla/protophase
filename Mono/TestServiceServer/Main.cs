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
            Registry registry = new Registry("tcp://localhost:5555");

            // Register our hello world responer object with the registry.
            HelloWorldResponder responder = new HelloWorldResponder();
            if(registry.Register("HelloWorldResponder", responder))
                Console.WriteLine("Registered HelloWorldResponder");
            else {
                Console.WriteLine("Failed to register HelloWorldResponder");
                return;
            }

            // Receive messages until quitting.
            while(!quit) {
                registry.Receive();
                Thread.Sleep(10);
            }

            // Unregister object
            if(registry.Unregister("HelloWorldResponder"))
                Console.WriteLine("Unregistered HelloWorldResponder");
            else
                Console.WriteLine("Failed to unregister HelloWorldResponder");
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
        [RPC]
        public String HelloWorld() {
            Console.WriteLine("Hello");
            return "World";
        }

        [RPC]
        public String ReturnParam(int par1, float par2, String par3) {
            return par1 + " " + par2 + " " + par3;
        }
    }
}

