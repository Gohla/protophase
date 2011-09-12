using System;
using Protophase.Service;

namespace Protophase.Examples {
    /**
    Common example application code that is used in every example.
    **/
    public abstract class ExampleApplication : IDisposable {
        protected Registry _registry;   ///< The Protophase registry client.

        /**
        Default constructor.
        **/
        public ExampleApplication() {
            Console.CancelKeyPress += CancelKeyPressHandler;
        }

        /**
        Finaliser.
        **/
        ~ExampleApplication() {
            Dispose();
        }

        /**
        Dispose of this object, cleaning up any resources it uses.
        **/
        public virtual void Dispose() {
            _registry.Dispose();

            GC.SuppressFinalize(this);
        }

        /**
        Starts the application. Calls the Init virtual method and then goes into the update loop.
        **/
        public void Start() {
            _registry = new Registry("tcp://localhost:5555");
            _registry.Idle += Idle;
            Init();
            _registry.AutoUpdate();
        }

        /**
        Called before going into the update loop.
        **/
        protected virtual void Init() { }
        /**
        Called after each update loop.
        **/
        protected virtual void Idle() { }

        /**
        Prevents application from quitting and stops the update loop.
        
        @param  sender  Source of the event.
        @param  args    Console cancel event arguments.
        **/
        private void CancelKeyPressHandler(object sender, ConsoleCancelEventArgs args) {
            args.Cancel = true; // Cancel quitting, do our own quitting.
            _registry.StopAutoUpdate();
        }
    }
}
