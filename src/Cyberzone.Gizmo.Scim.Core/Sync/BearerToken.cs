using System.Security.Cryptography;
using System.Text;

namespace Cyberzone.Gizmo.Scim.Core.Sync;

public static class BearerToken
{
    /// <summary>
    /// Checks an Authorization header value against the configured token in constant time.
    /// </summary>
    public static bool IsValid(string? authorizationHeader, string? expectedToken)
    {
        if (string.IsNullOrEmpty(expectedToken) || string.IsNullOrEmpty(authorizationHeader))
        {
            return false;
        }

        const string prefix = "Bearer ";
        if (!authorizationHeader!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var presented = Encoding.UTF8.GetBytes(authorizationHeader.Substring(prefix.Length).Trim());
        var expected = Encoding.UTF8.GetBytes(expectedToken!);
        return CryptographicOperations.FixedTimeEquals(presented, expected);
    }
}
