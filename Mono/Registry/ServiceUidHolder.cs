using System;
using System.Collections.Generic;
using Protophase.Shared;

namespace Protophase.Registry
{
    public class ServiceUidHolder
    {
        private DateTime _activity;
        public readonly List<ServiceInfo> Services;

        public ServiceUidHolder()
        {
            Services = new List<ServiceInfo>();
            Activity = DateTime.Now;
        }

        public DateTime Activity
        {
            get { return _activity; }
            set
            {
                _activity = value;
            }
        }
    }
}