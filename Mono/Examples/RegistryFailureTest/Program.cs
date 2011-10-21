using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protophase.Registry;

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

            var t1 = new Thread(r1.Start);
            var t2 = new Thread(r2.Start);
            var t3 = new Thread(r3.Start);

            t1.Start();
            t2.Start();
            t3.Start();

            while (!_stop)
            {
                Thread.Sleep(1);
            }

        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _stop = true;
        }
    }

    class RegistryServer
    {
        private readonly ushort _portRpc;
        private readonly ushort _portPub;

        private bool _stop;
        private int count = 0;

        private uint _connectRpc;
        private uint _connectPub;

        private Server _registryServer;

        public RegistryServer(ushort portRpc, ushort portPub, uint connectRpc, uint connectPub)
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
            _registryServer = new Server("*", _portRpc, _portPub);
            while (!_stop)
            {
                if (count % 1000 == 00)
                    _registryServer.DumpServerPool();
                if (_connectRpc > 0 && count == 2000)
                    _registryServer.AddToServerPool("localhost", _connectRpc, _connectPub);
                count++;
                _registryServer.Update();
                Thread.Sleep(1);
            }
        }
        public void Stop()
        {
            _registryServer.StopAutoUpdate();
        }
    }


}

