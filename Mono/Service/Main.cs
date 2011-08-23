using System;

namespace Service {
    class MainClass {
        public static void Main(string[] args) {
            Registry registry = new Registry("tcp://localhost:5555");
            Console.WriteLine("Registering service");
        }
    }
}
