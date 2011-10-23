namespace Protophase.Shared {
    /**
    Values that represent registry message types.
    **/
    public enum RegistryMessageType {
        RegisterApplication,
        RegisterService,
        UnregisterService,
        FindByUID,
        FindByType,
        Pulse,
        ServerPoolMessage
    }
}