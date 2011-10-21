using System;
using ZMQ;

namespace Protophase.Shared {
    /**
    Convenience class for dealing with ZMQ addresses.
    **/
    [Serializable]
    public class Address : IEquatable<Address> {
        private String _address;
        private Transport _transport = 0;
        private ushort _port = 0;

        /**
        Gets the ZMQ address.
        **/
        public String ZMQAddress { get { return _address; } }

        /**
        Gets the transport that is used in this address.
        **/
        public Transport Transport { get { return _transport; } }

        /**
        Gets the port that is used in this address. Defaults to 0 if no port is used.
        **/
        public ushort Port { get { return _port; } }

        /**
        Construct an address from a transport type, address and port.
        
        @param  transport   The transport type.
        @param  address     The (remote) address.
        @param  port        The port. Used in TCP, PGM and EPGM transports.
        **/
        public Address(Transport transport, String address, ushort port)
            : this(Enum.GetName(typeof(Transport), transport).ToLower() + "://" + address + ":" + port) {
                _port = port;
        }

        /**
        Constructor an address from a transport type and address. Can only be used for IPC and INPROC transport types.
        
        @exception  Exception   Thrown when given transport is not IPC or INPROC.
        
        @param  transport   The transport type.
        @param  address     The (remote) address.
        **/
        public Address(Transport transport, String address)
            : this(Enum.GetName(typeof(Transport), transport).ToLower() + "://" + address) {
                if(transport != Transport.IPC && transport != Transport.INPROC)
                    throw new System.Exception("Only IPC and INPROC transports can be used without a port.");

                _transport = transport;
        }


        /**
        Construct from a ZMQ address. Must be in the form of transport://address. (e.g. tcp://localhost:1337,
        inproc://test or ipc://tmp/feeds/0)
        
        @exception  Exception   Thrown when given address does not contain a transport.
        
        @param  address The ZMQ address.
        **/
        public Address(String address) {
            _address = address;

            // Derive the transport type.
            if(_transport != 0) {
                // TODO: Check existence.
                int transportPos = _address.IndexOf("://");
                String protocolName = _address.Substring(0, transportPos);
                Console.WriteLine(protocolName);
                _transport = (Transport)Enum.Parse(typeof(Transport), protocolName, true);
            }

            // Derive port
            if(_port == 0 && UsesPorts()) {
                // TODO: Check existence.
                int portPos = _address.LastIndexOf(':');
                Console.WriteLine(_address.Substring(portPos));
                _port = ushort.Parse(_address.Substring(portPos));
            }
        }

        /**
        Queries if this address uses ports.
        
        @return True if it uses ports, false otherwise.
        **/
        public bool UsesPorts() {
            return _transport == Transport.TCP || _transport == Transport.PGM || _transport == Transport.EPGM;
        }

        /**
        Tests if this object is equal to another.
        
        @param  other   The object check equality against.
        
        @return True if the objects are equal, false if they are not.
        **/
        public bool Equals(Address other) {
            return _address == other.ZMQAddress;
        }

        /**
        Calculates the hash code for this object.
        
        @return The hash code for this object.
        **/
        public override int GetHashCode() {
            return _address.GetHashCode();
        }

        /**
        Convert this object into a string representation.
        
        @return A string representation of this object.
        **/
        public override String ToString() {
            return _address;
        }
    }


    /**
    Extension methods for the Address class.
    **/
    public static class AddressExtensions {
                /**
        Connects to the given address.
        
        @param  socket  The socket to act on.
        @param  address The address to connect to.
        **/
        public static void Connect(this Socket socket, Address address)
        {
            // TODO: Validate address for connecting.
            socket.Connect(address.ZMQAddress);
        }

        /**
        Binds to the given address.
        
        @param  socket  The socket to act on.
        @param  address The address to bind to.
        **/
        public static void Bind(this Socket socket, Address address)
        {
            // TODO: Validate address for binding.
            socket.Bind(address.ZMQAddress);
        }
    }
}
