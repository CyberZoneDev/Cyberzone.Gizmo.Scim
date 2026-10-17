namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// Gizmo-version specific storage. Implemented separately for v2 and v3.
/// "Managed" users are the ones that carry the link attribute.
/// </summary>
public interface IGizmoUserStore
{
    /// <summary>
    /// Managed, not deleted user by Gizmo id, or null.
    /// </summary>
    GizmoUserProfile? FindManagedById(int id);

    /// <summary>
    /// Managed, not deleted user by link attribute value, or null.
    /// </summary>
    GizmoUserProfile? FindManagedByExternalId(string externalId);

    /// <summary>
    /// Managed, not deleted user by exact username (case-insensitive), or null.
    /// </summary>
    GizmoUserProfile? FindManagedByUsername(string username);

    /// <summary>
    /// Managed, not deleted users ordered by id.
    /// </summary>
    IReadOnlyList<GizmoUserProfile> ListManaged(int skip, int take, out int total);

    /// <summary>
    /// True when any user (managed or not, including operators) has this username.
    /// </summary>
    bool IsUsernameTaken(string username, int? exceptUserId);

    /// <summary>
    /// True when any user has this email.
    /// </summary>
    bool IsEmailTaken(string email, int? exceptUserId);

    /// <summary>
    /// Creates a customer in the default user group, sets the link attribute and returns its id.
    /// </summary>
    int Create(GizmoUserProfile profile);

    /// <summary>
    /// Updates username, email, names and phone. Does not touch group, billing or disabled state.
    /// </summary>
    void UpdateProfile(GizmoUserProfile profile);

    void SetDisabled(int id, bool disabled);

    /// <summary>
    /// Sets (non-null value) or removes (null value) user attributes.
    /// </summary>
    void SetAttributes(int id, IReadOnlyDictionary<string, string?> attributes);
}
