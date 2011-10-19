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
            var test = new RpcServiceFailureReplaceTest();
            new Thread(test.Start).Start();
            while (test.Running)
                Thread.Sleep(100);
        }
    }

    class RpcServiceFailureReplaceTest
    {
        public RpcServiceFailureReplaceTest()
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
                var rpcClient = new RpcClientTest();
                var rpcClientThread = new Thread(rpcClient.Start);
                rpcClientThread.Start();
                {
                    //Start a RpcServerTest, gracefully stop
                    var rpcServer = new RpcServerTest();
                    var rpcServerThread = new Thread(rpcServer.Start);
                    rpcServerThread.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    //Console.WriteLine("Started rpcServer. Letting it run 5 seconds.");
                    Thread.Sleep(5000);
                    //Console.WriteLine("Gracefully killing rpcServer.");
                    rpcServer.StopNeatly();
                    Console.WriteLine("#########################Expect Fail messages################### - Stopped");
                    Thread.Sleep(10000);
                }

                {
                    //Start another RpcServerTest, hard kill (as if connection fails, comp breaks down etc.)
                    Console.WriteLine("Starting second new rpcServer.");
                    var rpcServer2 = new RpcServerTest();
                    var rpcServerThread2 = new Thread(rpcServer2.Start);
                    rpcServerThread2.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    //Console.WriteLine("Let second rpcServer run 5 seconds.");
                    Thread.Sleep(5000);
                    //Console.WriteLine("Killing second rpcServer (causing timeout) and waiting ten seconds.");
                    rpcServerThread2.Interrupt();
                    Console.WriteLine("#########################Expect Fail messages################### - Killed");
                    Thread.Sleep(10000);
                }

                {
                    //Start another RpcServerTest  (as if connection fails, comp breaks down etc.)
                    Console.WriteLine("Starting a third rpcServer.");
                    var rpcServer3 = new RpcServerTest();
                    var rpcServerThread3 = new Thread(rpcServer3.Start);
                    rpcServerThread3.Start();
                    Console.WriteLine("#########################Expect Success messages###################");
                    //Console.WriteLine("Let third rpcServer run 5 seconds.");
                    Thread.Sleep(5000);
                    //Console.WriteLine("Hard Killing third rpcServer (causing timeout) and waiting ten seconds.");
                    rpcServerThread3.Interrupt();
                    Console.WriteLine("#########################Expect Fail messages################### - Killed");
                    Thread.Sleep(10000);
                }
                rpcClient.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            _running = false;
        }
    }

    //The Rpc exposed method. A running instance of this class is to be (violently) killed in order to analyse loss of RPC calls by the RpcClientTest class
    [ServiceType("RpcServerTest"), ServiceVersion("0.1")]
    class RpcServerTest
    {
        private Registry _registry;
        public void Start()
        {
            _registry = new Registry();
            _registry.Register(this);
            try
            {
                _registry.AutoUpdate(1);
                _registry.Unregister(this);
            }
            catch (Exception e)
            {
                Console.WriteLine("RpcServer error: " + e.Message);
            }
        }
        public void StopNeatly()
        {
            _registry.StopAutoUpdate();
        }
        [RPC]
        public String TestMethod(String param)
        {
            return DateTime.Now.ToLongTimeString() + " " + param;
        }
    }

    class RpcClientTest
    {
        private Registry _registry;
        private bool _stop;

        public void Start()
        {
            _registry = new Registry();
            int counter = 0;
            Service service = null;
            while (service == null)
                service = _registry.GetServiceByType("RpcServerTest");
            while (!_stop)
            {
                try
                {
                    Console.WriteLine("Successfully received : '" + service.Call<string>("TestMethod", counter.ToString()) + "'");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error when calling service: " + e.Message);
                }
                counter++;
                Thread.Sleep(1000);
                _registry.Update();
            }
        }
        public void Stop()
        {
            _stop = true;
        }
    }
}
