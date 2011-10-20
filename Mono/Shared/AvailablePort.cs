using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Linq;
using ZMQ;

namespace Protophase.Shared {
    /**
    Utility class for binding sockets
    **/
    public static class SocketExtension {
        public static readonly int MAXTRIES = 1000;
        private static readonly Random _random = new Random();

        private static ushort RandomInitialPort { get { return (ushort)(1024 + _random.Next(0, 1280) * 50); } }

        public static ushort BindAvailablePort(this Socket socket, Transport transport, String address) {
            ushort port = AvailablePort.Find(RandomInitialPort);
            int tries = MAXTRIES;

            // Retry binding socket until it succeeds.
            while(tries > 0) {
                try {
                    socket.Bind(transport, address, port);
                    break;
                } catch(ZMQ.Exception) {
                    port = AvailablePort.Find(RandomInitialPort);
                    --tries;
                }
            }

            return port;
        }
    }

    /**
    Utility class to find available network ports.
    **/
    public static class AvailablePort {
        private static readonly int MAXTRIES = 1000;
        private static readonly ushort USABLE_PORTS = ushort.MaxValue - 1024;
        private static readonly List<ushort> _availablePorts = new List<ushort>(USABLE_PORTS);
        private static ushort _listIterator = ushort.MaxValue;

        /**
        Gets active TCP connections. Recursive function because properties.GetActiveTcpConnections may throw an
        exception sometimes...
        
        @return The active TCP connections.
        **/
        public static TcpConnectionInformation[] GetActiveTcpConnections(IPGlobalProperties properties, int tries) {
            while(tries > 0) {
                try {
                    return properties.GetActiveTcpConnections();
                } catch(NetworkInformationException) {
                    return GetActiveTcpConnections(properties, tries - 1);
                }
            }

            return null;
        }

        private static bool RegenerateAvailablePorts() {
            // Get all ports that are in use.
            HashSet<int> activePorts = new HashSet<int>();

            // Get active TCP connections.
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] connections = GetActiveTcpConnections(properties, MAXTRIES);
            if(connections == null) return false;
            activePorts.UnionWith(  from n in connections
                                    where n.LocalEndPoint.Port >= 1024
                                    select n.LocalEndPoint.Port
                                );

            // Get active TCP listners - WCF service listening in TCP.
            IPEndPoint[] endPoints = properties.GetActiveTcpListeners();
            activePorts.UnionWith(  from n in endPoints
                                    where n.Port >= 1024
                                    select n.Port
                                );

            // Get active UDP listeners.
            endPoints = properties.GetActiveUdpListeners();
            activePorts.UnionWith(  from n in endPoints
                                    where n.Port >= 1024
                                    select n.Port
                                );

            // Generate the list of available ports.
            _availablePorts.Clear();
            _availablePorts.Capacity = USABLE_PORTS;
            for(ushort i = 1024; i <= USABLE_PORTS; ++i) {
                if(!activePorts.Contains(i))
                    _availablePorts.Add(i);
            }
            _listIterator = 0;

            return true;
        }

        /**
        Searches for the first available TPC/UDP port.
        
        @param  minimalPort The port number to start searching at.
        
        @return Available port (>= minimalPort), or 0 if no port is available.
        **/
        public static ushort Find(ushort minimalPort) {
            if(_listIterator > 5000) {
                lock(_availablePorts) {
                    if(!RegenerateAvailablePorts()) 
                        return 0;
                }
            }

            return _availablePorts[_listIterator++];
        }
    }
}

