using System;

namespace Protophase.Shared
{
    /*
     * This class makes sure there is one global GUID per application.
     * This GUID is sent with all packages to the registry service to make sure that all services belonging to a host which times out are removed from active pool.
     */
    [Serializable]
    public class ApplicationInstance
    {
        public static ApplicationInstance _appInstance;
        private static string _guid;
        public readonly string Guid;
        public ApplicationInstance()
        {
            if (_appInstance == null)
            {
                _guid = System.Guid.NewGuid().ToString();
                _appInstance = this;
            }
            Guid = _guid;
        }
    }
}