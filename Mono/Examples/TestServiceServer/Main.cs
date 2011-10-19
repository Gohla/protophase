using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace TestServiceServer {
    static class MainClass {
        private static bool _quit = false;

        public static void Main(string[] args) {
            // Get console quit key press events.
            Console.CancelKeyPress += CancelKeyPressHandler;

            // Create registry client for registry at address tcp://localhost:5555.
            using(Registry registry = new Registry("tcp://localhost:5555")) {
                // Register our hello world responder objects with the registry.
                HelloWorldResponder responder1 = new HelloWorldResponder(1);
                HelloWorldResponder responder2 = new HelloWorldResponder(2);
                HelloWorldResponder responder3 = new HelloWorldResponder(3);

                if(registry.Register("HelloWorldResponder1", responder1))
                    Console.WriteLine("Registered HelloWorldResponder1");
                else {
                    Console.WriteLine("Failed to register HelloWorldResponder 1");
                    return;
                }
                if(registry.Register("HelloWorldResponder2", responder2))
                    Console.WriteLine("Registered HelloWorldResponder2");
                else {
                    Console.WriteLine("Failed to register HelloWorldResponder2");
                    return;
                }
                if(registry.Register("HelloWorldResponder3", responder3))
                    Console.WriteLine("Registered HelloWorldResponder3");
                else {
                    Console.WriteLine("Failed to register HelloWorldResponder3");
                    return;
                }

                // Program loop
                while(!_quit) {
                    // Emit an event in the service.
                    responder1.SomethingHappened();
                    responder3.SomethingHappened();

                    // Receive RPC calls
                    // TODO: Optionally let the library take care of this.
                    registry.Update();

                    Thread.Sleep(100);
                }
            }
        }

        // Handler for console cancel key presses.
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _quit = true;
        }
    }

    // Simple hello world responder.
    [ServiceType("HelloWorldResponder"), ServiceVersion("0.1")]
    public class HelloWorldResponder {
        public int Number;

        [Publisher]
        public event PublishedDelegate Hello;
        [Publisher]
        public event PublishedDelegate World;

        public HelloWorldResponder(int number) {
            Number = number;
        }

        [RPC]
        public String HelloWorld() {
            Console.WriteLine("RPC called: HelloWorld");
            return "Hello world " + Number + "!";
        }

        [RPC]
        public String ReturnParam(int par1, float par2, String par3) {
            Console.WriteLine("RPC called: ReturnParam");
            return Number + " " + par1 + " " + par2 + " " + par3;
        }

        public void SomethingHappened() { 
            if(Hello != null) Hello("Hello world " + Number + "!");
            if(World != null) World("World hello " + Number + "!");
        }
    }
}

