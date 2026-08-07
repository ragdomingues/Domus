namespace Domus.Domain.Enums;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2
}

public enum UserStatus
{
    Active = 1,
    Locked = 2,
    Disabled = 3
}

public enum TenantRole
{
    Owner = 1,
    Admin = 2,
    Member = 3
}

public enum ResidenceRole
{
    Administrator = 1,
    Member = 2,
    Visitor = 3
}

public enum DeviceType
{
    Gate = 1,
    Light = 2,
    Sensor = 3,
    Lock = 4,
    Camera = 5,
    Other = 99
}

public enum DeviceConnectionStatus
{
    Unknown = 0,
    Offline = 1,
    Online = 2
}

public enum DeviceLifecycleStatus
{
    Created = 1,
    Provisioning = 2,
    Active = 3,
    Suspended = 4,
    Deleted = 5
}

public enum GateState
{
    Unknown = 0,
    Closed = 1,
    Open = 2,
    Moving = 3
}

public enum ProvisioningStatus
{
    Pending = 1,
    Activated = 2,
    Expired = 3,
    Revoked = 4
}

public enum CommandAction
{
    Open = 1,
    Close = 2,
    Stop = 3
}

public enum CommandSource
{
    MobileApp = 1,
    WebAdmin = 2,
    Automation = 3,
    System = 4,
    API = 5
}

public enum CommandStatus
{
    Pending = 1,
    Sent = 2,
    Delivered = 3,
    Executed = 4,
    Failed = 5,
    Expired = 6
}

public enum EventOrigin
{
    App = 1,
    Automation = 2,
    Admin = 3,
    System = 4
}

public enum EventResult
{
    Success = 1,
    Failure = 2,
    Pending = 3
}

public enum SecurityAuditAction
{
    LoginSucceeded = 1,
    LoginFailed = 2,
    Logout = 3,
    RefreshSucceeded = 4,
    RefreshReuseDetected = 5,
    RefreshRevoked = 6,
    RegisterSucceeded = 7,
    RateLimitTriggered = 8,
    IdorBlocked = 9,
    ProvisioningIssued = 10,
    ProvisioningActivated = 11,
    RoleChanged = 12,
    ResidenceCreated = 13,
    ResidenceUpdated = 14,
    ResidenceDeleted = 15,
    DeviceCreated = 16,
    DeviceUpdated = 17,
    DeviceDeleted = 18,
    ProvisioningFailed = 19,
    CommandCreated = 20,
    CommandFailed = 21,
    MemberInvited = 22,
    MemberRevoked = 23,
    PasswordResetRequested = 24,
    PasswordResetSucceeded = 25,
    PasswordResetFailed = 26
}
