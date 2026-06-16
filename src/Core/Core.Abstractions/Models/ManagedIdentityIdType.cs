namespace Core.Abstractions;

/// <summary>
/// Specifies how a user-assigned managed identity is identified.
/// Mirrors Azure.Identity.ManagedIdentityId factory methods.
/// </summary>
public enum ManagedIdentityIdType
{
    /// <summary>System-assigned managed identity (no ID needed).</summary>
    SystemAssigned,

    /// <summary>User-assigned, identified by client (application) ID.</summary>
    ClientId,

    /// <summary>User-assigned, identified by object (principal) ID.</summary>
    ObjectId,

    /// <summary>User-assigned, identified by ARM resource ID.</summary>
    ResourceId,
}
