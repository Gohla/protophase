using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Protophase.Service;
using Protophase.Shared;
using Protophase.Registry;
using ZMQ;

namespace RegistryStresstester
{
    public static class MainClass
    {
        public const int PUBLISHERS = 20;
        public static readonly Address SERVER_RPC_ADDRESS = new Address(Transport.INPROC, "ServerRPC");
        public static readonly Address SERVER_PUBLISH_ADDRESS = new Address(Transport.INPROC, "ServerPublish");

        private static readonly List<RegistryChurnNode> _churnNodes = new List<RegistryChurnNode>();
        private static readonly List<Thread> _churnNodeThreads = new List<Thread>();
        private static Server _server;
        private static Thread _serverThread;
        private static bool _stop = false;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressHandler;

            // Create server
            Console.WriteLine("Starting registry server on: " + SERVER_RPC_ADDRESS + " " + SERVER_PUBLISH_ADDRESS);
            _server = new Server(SERVER_RPC_ADDRESS, SERVER_PUBLISH_ADDRESS);
            _serverThread = new Thread(_server.AutoUpdate);
            _serverThread.Start();
            Thread.Sleep(2000);

            // Create some worker classes and put them to work
            for (int i = 0; i < PUBLISHERS; i++)
            {
                if(_stop) 
                    break;

                RegistryChurnNode churnNode = new RegistryChurnNode();
                _churnNodes.Add(churnNode);

                Thread thread = new Thread(churnNode.StartChurning);
                _churnNodeThreads.Add(thread);
                thread.Start();

                Thread.Sleep(1000);
            }

            // Wait for all threads to stop.
            _serverThread.Join();
            foreach(Thread thread in _churnNodeThreads)
            {
                thread.Join();
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _stop = true;

            Console.WriteLine("Cleaning up...");
            _server.StopAutoUpdate();
            foreach(var x in _churnNodes)
                x.Stop();
        }
    }

    class RegistryChurnNode
    {
        private readonly Random _random = new Random();
        private readonly Registry _registry;
        private bool _stop;
        private double _noUnregisterChance;

        public RegistryChurnNode(double noUnregisterChance)
        {
            if (noUnregisterChance < 0 || noUnregisterChance > 1)
                throw new System.Exception("noUnregisterChance must be in [0,1]");

            _registry = new Registry(MainClass.SERVER_RPC_ADDRESS, MainClass.SERVER_PUBLISH_ADDRESS, "");
            _noUnregisterChance = noUnregisterChance;

            Console.WriteLine("Created new Registry Churner");
        }

        public RegistryChurnNode() : this(0.0) { }

        public void StartChurning()
        {
            while (!_stop)
            {
                DummyService dummy = new DummyService();
                _registry.Register(dummy);
   
                Thread.Sleep(_random.Next(50, 200));

                if(_random.NextDouble() >= _noUnregisterChance)
                    _registry.Unregister(dummy);

                _registry.Update();
            }
        }

        public void Stop()
        {
            Console.WriteLine("Stopping Churn node");
            _stop = true;
        }
    }

    [ServiceType("DummyService"), ServiceVersion("0.1")]
    public class DummyService
    {

    }
}
