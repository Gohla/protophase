using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using Protophase.Registry;
using Protophase.Service;
using Protophase.Shared;
using ZMQ;

namespace RegistryFailureTest
{
    class Program
    {
        private static bool _stop;
        static void Main(string[] args)
        {
            Console.CancelKeyPress += new ConsoleCancelEventHandler(Console_CancelKeyPress);
            var r1 = new RegistryServer(4000, 4001);
            var r2 = new RegistryServer(5000, 5001, 4000, 4001);
            var r3 = new RegistryServer(6000, 6001, 4000, 4001);
            var r4 = new RegistryServer(7000, 7001, 6000, 6001);

            var c1 = new RegistryClient();
            var c2 = new RegistryClient();

            var t1 = new Thread(r1.Start);
            var t2 = new Thread(r2.Start);
            var t3 = new Thread(r3.Start);
            var t4 = new Thread(r4.Start);

            var serviceThread1 = new Thread(c1.Start);
            var serviceThread2 = new Thread(c2.Start);

            t1.Start();
            t2.Start();
            Thread.Sleep(10000);
            serviceThread1.Start();
            serviceThread2.Start();
            Thread.Sleep(5000);
            //c1._stop = true;
            t3.Start();
            t4.Start();
            Thread.Sleep(10000);
            t1.Interrupt();
            t2.Interrupt();
            t3.Interrupt();
            

            while (!_stop)
                Thread.Sleep(1);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _stop = true;
        }
    }

    [ServiceType("PubServerT"), ServiceVersion("0.1")]
    class RegistryClient
    {
        private Registry _registry;
        public bool _stop;
        [Publisher]
        public event PublishedDelegate TestMethod;
        public void Start()
        {
            _registry = new Registry(new Address(Transport.TCP, "localhost", 4000), new Address(Transport.TCP, "localhost", 4001), "localhost");
            _registry.Register(this);
            while (!_stop)
            {
                Thread.Sleep(10);
                _registry.Update();
            }
            _registry.Unregister(this);
        }
    }

    class RegistryServer
    {
        private readonly ushort _portRpc;
        private readonly ushort _portPub;

        private bool _stop;
        private int count = 0;

        private ushort _connectRpc;
        private ushort _connectPub;

        private Server _registryServer;

        public RegistryServer(ushort portRpc, ushort portPub, ushort connectRpc, ushort connectPub)
            :this (portRpc, portPub)
        {
            _connectRpc = connectRpc;
            _connectPub = connectPub;

        }

        public RegistryServer(ushort portRpc, ushort portPub)
        {
            _portRpc = portRpc;
            _portPub = portPub;
        }

        public void Start()
        {
            _registryServer = new Server(new Address(Transport.TCP, "*", _portRpc), new Address(Transport.TCP, "*", _portPub));
            try
            {
                while (!_stop)
                {
                    if (count%10000 == 0)
                        _registryServer.DumpPool();
                    if (_connectRpc > 0 && count == 2000)
                        _registryServer.AddToServerPool(
                            new Address(Transport.TCP, "localhost", _connectRpc),
                            new Address(Transport.TCP, "localhost", _connectPub),
                            new Address(Transport.TCP, "localhost", _portRpc),
                            new Address(Transport.TCP, "localhost", _portPub)
                            );
                    count++;
                    _registryServer.Update();
                    Thread.Sleep(1);
                }
            } catch(System.Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        public void Stop()
        {
            _registryServer.StopAutoUpdate();
        }
    }


}

