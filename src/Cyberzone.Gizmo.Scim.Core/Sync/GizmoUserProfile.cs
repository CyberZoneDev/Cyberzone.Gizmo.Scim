namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// Version-independent view of a Gizmo customer account, limited to synchronized fields.
/// </summary>
public sealed class GizmoUserProfile
{
    public int Id { get; set; }

    /// <summary>Value of the link attribute (SCIM externalId).</summary>
    public string? ExternalId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MobilePhone { get; set; }

    public bool IsDisabled { get; set; }

    public DateTime? CreatedTime { get; set; }

    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// Gizmo user attributes (without the link attribute).
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
