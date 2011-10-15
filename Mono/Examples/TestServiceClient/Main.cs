using System;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;

namespace TestServiceClient {
    static class MainClass {
        private static bool _quit = false;

        public static void Main(string[] args) {
            try {
                // Get console quit key press events.
                Console.CancelKeyPress += CancelKeyPressHandler;

                // Sleep so registry and server can start up first.
                Thread.Sleep(1000);

                // Create registry client for registry at address tcp://localhost:5555.
                using(Registry registry = new Registry()) 
                {
                    // Get HelloWorldResponder service, loop until it exists.
                    Service helloWorld1 = null;
                    while(helloWorld1 == null) helloWorld1 = registry.GetServiceByUID("HelloWorldResponder1");
                    Console.WriteLine("Found HelloWorldResponder1: " + helloWorld1);

                    // Get HelloWorldResponder services, loop until it exists.
                    Service helloWorldAll = null;
                    while(helloWorldAll == null) helloWorldAll = registry.GetServiceByType("HelloWorldResponder");
                    Console.WriteLine("Found HelloWorldResponders: " + helloWorldAll);

                    // Subscribe for published event.
                    helloWorld1.Published += (object obj) => Console.WriteLine("HelloWorldResponder1 published: " + obj);
                    helloWorldAll.Published += (object obj) => Console.WriteLine("HelloWorldResponders published: " + obj);

                    // Program loop
                    while(!_quit) {
                        // Call receive on the objects to receive published messages.
                        // TODO: Optionally let the library take care of this.
                        //helloWorld1.Receive();
                        //helloWorldAll.Receive();
                        registry.Update();
                        //Console.WriteLine(ApplicationInstance.Guid);

                        // RPC HelloWorld function with no parameters and print the result.
                        //Console.WriteLine("RPC call HelloWorld on HelloWorldResponder1: " + helloWorld1.Call<String>("HelloWorld"));
                        //Console.WriteLine("RPC call HelloWorld on HelloWorldResponders: " + helloWorldAll.Call<String>("HelloWorld"));
                        // RPC ReturnParam function with 3 params and print the result.
                        //Console.WriteLine("RPC call ReturnParam on HelloWorldResponder1: " + helloWorld1.Call<String>("ReturnParam", 1, 1.0f, "one"));
                        //Console.WriteLine("RPC call ReturnParam on HelloWorldResponders: " + helloWorldAll.Call<String>("ReturnParam", 1, 1.0f, "one"));

                        Thread.Sleep(100);
                    }

                    // Dispose of service objects to free sockets.
                    helloWorld1.Dispose();
                    helloWorldAll.Dispose();
                }
            } catch(Exception e) {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                Thread.Sleep(10000);
            }
        }

        // Handler for console cancel key presses.
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _quit = true;
        }
    }
}

