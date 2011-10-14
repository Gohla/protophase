using System;

namespace Protophase.Shared
{
    /*
     * This class makes sure there is one global GUID per application.
     * This GUID is sent with all packages to the registry service to make sure that all services belonging to a host which times out are removed from active pool.
     */
    [Serializable]
    public static class ApplicationInstance
    {
        private static String _guid;
        public static string Guid 
        {
            get 
            {
                if (_guid == null)
                    _guid = System.Guid.NewGuid().ToString();

                return _guid;
            }
        }
    }
}