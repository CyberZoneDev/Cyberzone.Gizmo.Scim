namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// Column sizes of the Gizmo v2 User/UserMember tables.
/// </summary>
public static class FieldLimits
{
    public const int Email = 254;
    public const int FirstName = 45;
    public const int LastName = 45;
    public const int MobilePhone = 20;
    public const int AttributeValue = 255;
    public const int AttributeName = 45;

    /// <summary>
    /// Clips a string to the specified maximum length.
    /// </summary>
    public static string? Clip(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value!.Trim();
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
