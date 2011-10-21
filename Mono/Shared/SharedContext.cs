using System;
using ZMQ;

namespace Protophase.Shared
{
    static class SharedContext
    {
        private static readonly Context _sharedContext = new Context(1);

        public static Context Context { get { return _sharedContext; } }
    }
}
