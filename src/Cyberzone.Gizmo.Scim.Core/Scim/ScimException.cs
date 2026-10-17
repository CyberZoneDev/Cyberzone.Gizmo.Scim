namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// Error that is returned to the SCIM client as a SCIM error response.
/// </summary>
public sealed class ScimException : Exception
{
    public ScimException(int status, string? scimType, string detail)
        : base(detail)
    {
        Status = status;
        ScimType = scimType;
    }

    public int Status { get; }

    /// <summary>
    /// SCIM error type (invalidFilter, invalidValue, uniqueness, ...).
    /// </summary>
    public string? ScimType { get; }

    public static ScimException BadRequest(string scimType, string detail) => new(400, scimType, detail);

    public static ScimException NotFound(string detail) => new(404, null, detail);
}
