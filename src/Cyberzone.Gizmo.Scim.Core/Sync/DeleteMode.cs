namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// What SCIM DELETE does to the Gizmo user.
/// </summary>
public enum DeleteMode
{
    /// <summary>
    /// Disable the user. The account, balance and profile folder stay as they are.
    /// </summary>
    Disable,

    /// <summary>
    /// Gizmo soft delete (IsDeleted). Gizmo also removes the user profile folder from disk,
    /// which cannot be restored. A later POST with the same externalId undeletes the account.
    /// </summary>
    Delete,
}
