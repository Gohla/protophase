using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protophase.Service;
using System.IO;
using Protophase.Shared;

namespace RegistryStresstester
{
    class RegistryStressTester
    {
        private const int PUBLISHERCOUNT = 20;
        private static bool _stop;
        private static readonly List<RegistryChurnNode> _churnNodes = new List<RegistryChurnNode>();
        private static readonly List<Thread> _churnNodeThreads = new List<Thread>();

        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressHandler;
            /* Create some worker classes and put them to work
             */
            for (int i = 0; i < PUBLISHERCOUNT; i++)
            {
                if (_stop)
                    break;
                RegistryChurnNode churnNode = new RegistryChurnNode();
                _churnNodes.Add(churnNode);
                var thread = new Thread(churnNode.StartChurning);
                _churnNodeThreads.Add(thread);
                thread.Start();
                Thread.Sleep(500);
            }

            while (!_stop) //keep app running...
            {
                Thread.Sleep(1);
            }
        }

        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("Cleaning up...");
            foreach (var x in _churnNodes)
                x.Stop();
            _stop = true;
        }
    }



    class RegistryChurnNode
    {
        private readonly Registry _registry;
        private bool _stop;
        private double _noUnregisterChance;

        public RegistryChurnNode(double noUnregisterChance)
        {
            if (noUnregisterChance < 0 || noUnregisterChance > 1)
                throw new Exception("noUnregisterChance must be in [0,1]");
            _noUnregisterChance = noUnregisterChance;
            _registry = new Registry(Constants.REGISTRY_URL);
            Console.WriteLine("Created new Registry Churner");
        }

        public RegistryChurnNode() : this(0)
        {
        }

        public void StartChurning()
        {
            while (!_stop)
            {
                string publishName = "Random" + new Random().Next(0,Int32.MaxValue);
                DummyService d = new DummyService();
                _registry.Register(publishName, d);
                for (int i = 0; i < 50; i++)
                    _registry.Publish(publishName, publishName);
                Thread.Sleep(new Random().Next(500, 2000));

                if (new Random().NextDouble() >= _noUnregisterChance)
                    _registry.Unregister(publishName);

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
