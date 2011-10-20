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
        public static readonly int MAXTRIES = 1000;

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

        /**
        Searches for the first available TPC/UDP port.
        
        @param  minimalPort The port number to start searching at.
        
        @return Available port (>= minimalPort), or 0 if no port is available.
        **/
        public static ushort Find(ushort minimalPort) {
            List<int> portArray = new List<int>();
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            
            // Get active TCP connections
            TcpConnectionInformation[] connections = GetActiveTcpConnections(properties, MAXTRIES);
            if(connections == null) return 0;
            portArray.AddRange(from n in connections
                               where n.LocalEndPoint.Port >= minimalPort
                               select n.LocalEndPoint.Port);

            // Get active TCP listners - WCF service listening in TCP
            IPEndPoint[] endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints
                where n.Port >= minimalPort
                select n.Port);
            
            // Get active UDP listeners
            endPoints = properties.GetActiveUdpListeners();
            portArray.AddRange(from n in endPoints
                where n.Port >= minimalPort
                select n.Port);
            
            portArray.Sort();
            
            for(int i = minimalPort; i < UInt16.MaxValue; i++)
                if(!portArray.Contains(i))
                    return (ushort)i;
            
            return 0;
        }
    }
}

