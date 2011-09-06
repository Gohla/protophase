using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace TestServiceClient {
    static class MainClass {
        private static bool quit = false;

        public static void Main(string[] args) {
            try {
                // Get console quit key press events.
                Console.CancelKeyPress += CancelKeyPressHandler;

                // Create registry client for registry at address tcp://localhost:5555.
                using(Registry registry = new Registry("tcp://localhost:5555")) {
                    // Get HelloWorldResponder service, loop until it exists.
                    Service helloWorld = null;
                    while(helloWorld == null) helloWorld = registry.GetService("HelloWorldResponder");
                    Console.WriteLine("Found HelloWorldResponder: " + helloWorld);

                    using(helloWorld) {
                        // Subscribe for published event.
                        helloWorld.Published += (object obj) => Console.WriteLine("Published: " + obj);

                        // Program loop
                        while(!quit) {
                            // Call receive on the object to receive published messages.
                            // TODO: Optionally let the library take care of this.
                            helloWorld.Receive();

                            // RPC HelloWorld function with no parameters and print the result.
                            Console.WriteLine(helloWorld.Call<String>("HelloWorld"));
                            // RPC ReturnParam function with 3 params and print the result.
                            Console.WriteLine(helloWorld.Call<String>("ReturnParam", 1, 1.0f, "one"));

                            Thread.Sleep(10);
                        }
                    }
                }
            } catch(Exception e) {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
            }
        }

        // Handler for console cancel key presses.
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            quit = true;
        }
    }
}

