using System.Text;

namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// Turns an arbitrary identity-provider username into one Gizmo accepts.
/// Gizmo v2 rules (Service.UserCrateAsync + ValidationFileInvalidCharacters):
/// max 30 chars, no spaces, no Windows file-name characters, no trailing '.',
/// not a reserved device name, unique.
/// </summary>
public static class UsernameNormalizer
{
    public const int MaxLength = 30;

    private const string Fallback = "user";

    // Windows Path.GetInvalidFileNameChars(): control chars plus these. Hard-coded so tests behave the same on Linux.
    private static readonly HashSet<char> InvalidChars = new("\"<>|:*?\\/");

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Applies the character and length rules (without uniqueness).
    /// </summary>
    public static string Normalize(string? username)
    {
        var builder = new StringBuilder();
        var lastWasUnderscore = false;

        foreach (var c in (username ?? string.Empty).Trim())
        {
            var mapped = char.IsWhiteSpace(c) || char.IsControl(c) || InvalidChars.Contains(c) ? '_' : c;
            if (mapped == '_' && lastWasUnderscore)
            {
                continue;
            }

            builder.Append(mapped);
            lastWasUnderscore = mapped == '_';
        }

        var result = Fit(builder.ToString().Trim('_'), MaxLength);
        if (result.Length == 0)
        {
            result = Fallback;
        }

        if (IsReserved(result))
        {
            result = Fit(result, MaxLength - 1) + "_";
        }

        return result;
    }

    /// <summary>
    /// Applies the rules and appends _2, _3, ... until <paramref name="isTaken"/> returns false.
    /// </summary>
    public static string MakeUnique(string? username, Func<string, bool> isTaken)
    {
        var baseName = Normalize(username);
        if (!isTaken(baseName))
        {
            return baseName;
        }

        for (var i = 2; i < 10000; i++)
        {
            var suffix = "_" + i;
            var candidate = Fit(baseName, MaxLength - suffix.Length) + suffix;
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not find a free username for '{username}'.");
    }

    public static bool IsValid(string? username) =>
        !string.IsNullOrEmpty(username) && Normalize(username) == username;

    // Cuts to the length and removes characters Gizmo does not allow at the end.
    private static string Fit(string value, int maxLength)
    {
        if (value.Length > maxLength)
        {
            value = value.Substring(0, maxLength);
            if (char.IsHighSurrogate(value[^1]))
            {
                value = value.Substring(0, value.Length - 1);
            }
        }

        return value.TrimEnd('.', ' ');
    }

    private static bool IsReserved(string value)
    {
        var dot = value.IndexOf('.');
        var withoutExtension = dot >= 0 ? value.Substring(0, dot) : value;
        return ReservedNames.Contains(value) || ReservedNames.Contains(withoutExtension);
    }
}
