namespace Cyberzone.Gizmo.Scim.Core.Sync;

public sealed class ScimSettings
{
    /// <summary>
    /// Static token configured in the Authentik SCIM provider (Authorization: Bearer ...).
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;

    /// <summary>
    /// Name of the Gizmo user attribute that stores the SCIM externalId.
    /// </summary>
    public string LinkAttributeName { get; set; } = "AuthentikId";

    /// <summary>
    /// Gizmo attributes that may be set through the extension schema.
    /// Empty list means no attributes are synchronized.
    /// </summary>
    public List<string> AllowedAttributes { get; set; } = new();

    /// <summary>
    /// Public URL of the SCIM endpoint as Authentik sees it (behind the reverse proxy),
    /// for example https://gizmo.example.com/scim/v2. Empty = taken from the request.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// When the email already belongs to another Gizmo user: false = leave email empty, true = reject with 409.
    /// </summary>
    public bool RejectDuplicateEmail { get; set; }

    /// <summary>
    /// What DELETE /Users/{id} does: Disable (default) or Delete (Gizmo soft delete, removes the profile folder).
    /// </summary>
    public DeleteMode DeleteMode { get; set; } = DeleteMode.Disable;

    /// <summary>
    /// Give users without a password a random one nobody knows. Gizmo lets a customer without a password
    /// log in with any password, so false leaves synced accounts open to anyone who knows the username.
    /// </summary>
    public bool SetRandomPassword { get; set; } = true;
}
