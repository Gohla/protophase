using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;


namespace RPCServiceFailureReplace
{
    /* There are four classes in this file
     * Program (to run the test, simply a main)
     * RpcServiceFailureReplaceTest - Actually runs the test
     * RpcServerTest - The to be instantiated RpcServer (callee)
     * RpcClientTest - The to be instantiated RpcClient (caller)
     * 
     * The test in this file starts a rpc client (caller) which tries to call a remote method every second. (message index is sent along)
     * The second part of the test serially starts three Server processes (callees) which die in various manners
     * 
     * The goal is to see the rpcClient (caller) be successfully directed to a new instance of a same type'd service. (RpcServerTest)
     */
    class Program
    {
        static void Main(string[] args)
        {
            var test = new PubSubServiceFailureReplaceTest();
            new Thread(test.Start).Start();
            while (test.Running)
                Thread.Sleep(100);
        }
    }

    class PubSubServiceFailureReplaceTest
    {
        public PubSubServiceFailureReplaceTest()
        {
            _running = true;
        }

        private bool _running;
        public bool Running
        {
            get { return _running; }
        }
        public void Start()
        {
            _running = true;
            try
            {
                Console.WriteLine("Starting Publish/Subscribe Testcase 1");
                var subClient = new SubClientTest();
                var subClientThread = new Thread(subClient.Start);
                subClientThread.Start();
                {
                    var pubServer = new PubServerTest();
                    var rpcServerThread = new Thread(pubServer.Start);
                    rpcServerThread.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    Thread.Sleep(5000);
                    pubServer.StopNeatly();
                    Console.WriteLine("#########################Expect nothing################### - Stopped");
                    Thread.Sleep(10000);
                }

                {
                    //Start another RpcServerTest, hard kill (as if connection fails, comp breaks down etc.)
                    Console.WriteLine("Starting second new pubServer.");
                    var pubServer2 = new PubServerTest();
                    var pubServerThread2 = new Thread(pubServer2.Start);
                    pubServerThread2.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    Thread.Sleep(5000);
                    pubServerThread2.Interrupt();
                    Console.WriteLine("#########################Expect nothing################### - Killed");
                    Thread.Sleep(10000);
                }

                {
                    //Start another RpcServerTest  (as if connection fails, comp breaks down etc.)
                    Console.WriteLine("Starting a third pubServer.");
                    var pubServer3 = new PubServerTest();
                    var pubServerThread3 = new Thread(pubServer3.Start);
                    pubServerThread3.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    //Console.WriteLine("Let third rpcServer run 5 seconds.");
                    Thread.Sleep(5000);
                    //Console.WriteLine("Hard Killing third rpcServer (causing timeout) and waiting ten seconds.");
                    pubServerThread3.Interrupt();
                    Console.WriteLine("#########################Expect nothing################### - Killed");
                    Thread.Sleep(10000);
                }
                subClient.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            _running = false;
        }
    }

    //The Rpc exposed method. A running instance of this class is to be (violently) killed in order to analyse loss of RPC calls by the RpcClientTest class
    [ServiceType("PubServerTest"), ServiceVersion("0.1")]
    class PubServerTest
    {
        [Publisher]
        public event PublishedDelegate PublishedMethod;

        private bool _stop;
        private Registry _registry;
        public void Start()
        {
            _registry = new Registry();
            _registry.Register(this);
            int counter = 0;
            try
            {
                while (!_stop)
                {
                    if (counter % 100 == 0)
                        if (PublishedMethod != null)
                            PublishedMethod("Publising " + DateTime.Now.ToLongTimeString() + (counter / 100).ToString());
                    Thread.Sleep(10);
                    counter++;
                    _registry.Update();
                }
                _registry.Unregister(this);
            }
            catch (Exception e)
            {
                Console.WriteLine("PublishServer error: " + e.Message);
            }
        }

        public void StopNeatly()
        {
            _stop = true;
        }
    }

    class SubClientTest
    {
        private Registry _registry;
        private bool _stop;

        public void Start()
        {
            _registry = new Registry();
            Service service = null;
            while (service == null)
                service = _registry.GetServiceByType("PubServerTest");
            service.Published += Published;
            _registry.AutoUpdate(1);
        }

        private void Published(object obj)
        {
            Console.WriteLine("Published object received='" + obj + "'");
        }

        public void Stop()
        {
            _registry.StopAutoUpdate();
        }


    }
}
