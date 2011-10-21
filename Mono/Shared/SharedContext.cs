using System;
using System.Diagnostics;
using ZMQ;

namespace Protophase.Shared
{
    /**
    Static class for sharing an ZMQ context.
    **/
    public static class SharedContext {
        private static Object _lock = new Object();
        private static Context _sharedContext;
        private static uint _count = 0;

        /**
        Gets the shared context. Only call this once and store the return value, otherwise the Context will not be
        disposed when the application is closed.
        
        @return The shared context.
        **/
        public static Context Get() {
            lock(_lock) {
                if(++_count == 1)
                    _sharedContext = new Context(1);
            }

            return _sharedContext;
        }

        /**
        Disposes of the shared context. Call this once when you do not need the shared context anymore. When no
        object needs the shared context anymore it will be disposed.
        **/
        public static void Dispose() {
            lock(_lock) {
                Debug.Assert(_count != 0, "Trying to dispose of the shared context while it is already disposed.");
                if(--_count == 0) {
                    _sharedContext.Dispose();
                    _sharedContext = null;
                }
            }
        }
    }
}
