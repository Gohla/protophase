using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading;
using Protophase.Service;
using Protophase.Shared;



namespace RPCServiceFailureReplace
{

    //Errors I get:

    /* Occasionally:
     * Subscriber error: Binary stream '205' does not contain a valid BinaryHeader. Pos
        sible causes are invalid stream or object version change between serialization a
        nd deserialization.
     * 
     * Often:
     * Bad address (originating from Service.cs.Recieve() (164))
     */

    class Program
    {
        //Added this static field to easily set the sleep time for Update methods.
        //when this value is tweaked the library becomes unstable.
        public static readonly int WAITMS_Publisher = 1; //No sleep in publisher SEEMS to work without problems.
        public static readonly int WAITMS_Subscriber = 1;


        static void Main(string[] args)
        {
            Console.CancelKeyPress += CancelKeyPressHandler;
            var test = new PubSubServiceFailureReplaceTest();
            new Thread(test.Start).Start();
            while (test.Running)
                Thread.Sleep(100);
        }
        // Handler for console cancel key presses.
        private static void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true; // Cancel quitting, do our own quitting.
        }
    }

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
            int publishCounter = 0;
            DateTime lastTime = DateTime.MinValue;
            try
            {
                while (!_stop)
                {
                    if (lastTime.AddSeconds(1) < DateTime.Now) //every second
                    {
                        if (PublishedMethod != null)
                        {
                            PublishedMethod("Publising " + DateTime.Now.ToLongTimeString() + " - " + publishCounter.ToString());
                            publishCounter++;
                            lastTime = DateTime.Now;
                        }
                    }
                    Thread.Sleep(Program.WAITMS_Publisher);
                    _registry.Update();
                }
                _registry.Unregister(this);
            }
            catch (Exception)
            {
                //   throw;
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
        public void Start()
        {
            _registry = new Registry();
            Service service = null;
            while (service == null)
                service = _registry.GetServiceByType("PubServerTest");
            service.Published += Published;
            _registry.AutoUpdate(Program.WAITMS_Subscriber);
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
     


}
