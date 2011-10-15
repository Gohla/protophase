namespace Protophase.Shared {
    /**
    Values that represent registry message types.
    **/
    public enum RegistryMessageType {
        RegisterApplication,
        RegisterNamedService,
        RegisterService,
        UnregisterService,
        FindByUID,
        FindByType,
        Pulse
    }
}