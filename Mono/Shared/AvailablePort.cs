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
        public static readonly ushort INITIALPORT = 1024;
        public static readonly int MAXTRIES = 1000;

        public static ushort BindAvailablePort(this Socket socket, Transport transport, String address) {
            ushort port = AvailablePort.Find(INITIALPORT);
            int tries = MAXTRIES;

            // Retry binding socket until it succeeds.
            while(tries > 0) {
                try {
                    socket.Bind(transport, address, port);
                    break;
                } catch(ZMQ.Exception) {
                    port = AvailablePort.Find(INITIALPORT);
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
        /**
        Searches for the first available TPC/UDP port.
        
        @param  minimalPort The port number to start searching at.
        
        @return Available port (>= minimalPort), or 0 if no port is available.
        **/
        public static ushort Find(ushort minimalPort) {
            List<int> portArray = new List<int>();
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            
            // Get active connections
            // TODO: System.Net.NetworkInformation.NetworkInformationException is thrown here sometimes..
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            portArray.AddRange(from n in connections
                where n.LocalEndPoint.Port >= minimalPort
                select n.LocalEndPoint.Port);

            // Get active tcp listners - WCF service listening in tcp
            IPEndPoint[] endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(from n in endPoints
                where n.Port >= minimalPort
                select n.Port);
            
            // Get active udp listeners
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

