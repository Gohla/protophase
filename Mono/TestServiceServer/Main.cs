using System;
using System.Text;
using System.Threading;
using ZMQ;

namespace TestServiceServer {
    class MainClass {
        public static void Main(string[] args) {
            // ZMQ Context
            using(Context context = new Context(1)) {
                // Socket to talk to clients
                using(Socket socket = context.Socket(SocketType.REP)) {
                    socket.Bind("tcp://*:5555");
                    
                    while(true) {
                        // Wait for next request from client
                        string message = socket.Recv(Encoding.Unicode);
                        Console.WriteLine("Received request: {0}", message);
                        
                        // Do Some 'work'
                        Thread.Sleep(1000);
                        
                        // Send reply back to client
                        socket.Send("World", Encoding.Unicode);
                    }
                }
            }
        }
    }
}

