using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Linq;

namespace Protophase.Shared {
    public static class AvailablePort {
        public static ushort Find(ushort minimalPort) {
            List<int> portArray = new List<int>();
            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            
            // Get active connections
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

