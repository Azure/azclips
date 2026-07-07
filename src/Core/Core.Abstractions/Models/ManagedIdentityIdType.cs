namespace Core.Abstractions;

/// <summary>Managed identity selector type.</summary>
public enum ManagedIdentityIdType
{
    /// <summary>System-assigned identity.</summary>
    SystemAssigned,

    /// <summary>User-assigned identity by client ID.</summary>
    ClientId,

    /// <summary>User-assigned identity by object ID.</summary>
    ObjectId,

    /// <summary>User-assigned identity by resource ID.</summary>
    ResourceId,
}
