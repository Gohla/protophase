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
                Registry registry = new Registry("tcp://localhost:5555");
    
                // Get HelloWorldResponder service
                Service helloWorld = registry.GetService("HelloWorldResponder");
                Console.WriteLine("Found HelloWorldResponder: " + helloWorld);
    
                // Call HelloWorld function on HelloWorldResponder until quitting.
                while(!quit) {
                    Console.WriteLine(helloWorld.Call<String>("HelloWorld"));
                    Console.WriteLine(helloWorld.Call<String>("ReturnParam", 1, 1.0f, "one"));
                    Thread.Sleep(10);
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

