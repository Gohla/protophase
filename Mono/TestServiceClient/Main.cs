using System;
using System.Text;
using ZMQ;

namespace TestServiceClient {
    class MainClass {
        public static void Main(string[] args) {
            // ZMQ Context
            using (Context context = new Context(1)) {
                //  Socket to talk to server
                using (Socket requester = context.Socket(SocketType.REQ)) {
                    requester.Connect("tcp://localhost:5555");

                    string request = "Hello";
                    for (int requestNbr = 0; requestNbr < 10; requestNbr++) {
                        Console.WriteLine("Sending request {0}...", requestNbr);
                        requester.Send(request, Encoding.Unicode);

                        string reply = requester.Recv(Encoding.Unicode);
                        Console.WriteLine("Received reply {0}: {1}", requestNbr, reply);
                    }
                }
            }
        }
    }
}

