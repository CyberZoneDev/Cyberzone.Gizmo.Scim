using System.Text.RegularExpressions;

namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// Minimal SCIM filter: a single "attribute eq value" expression.
/// That is all Authentik sends (userName eq "..."), and all we advertise.
/// </summary>
public sealed class ScimFilter
{
    private static readonly Regex Expression = new(
        @"^\s*(?<attr>[A-Za-z][\w.:]*)\s+eq\s+(?:""(?<str>(?:[^""\\]|\\.)*)""|(?<raw>\S+))\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private ScimFilter(string attribute, string value)
    {
        Attribute = attribute;
        Value = value;
    }

    /// <summary>
    /// Attribute name as sent, for example "userName".
    /// </summary>
    public string Attribute { get; }

    public string Value { get; }

    public static ScimFilter? Parse(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        var match = Expression.Match(filter);
        if (!match.Success)
        {
            throw ScimException.BadRequest("invalidFilter", "Only 'attribute eq \"value\"' filters are supported.");
        }

        var value = match.Groups["str"].Success
            ? Regex.Unescape(match.Groups["str"].Value)
            : match.Groups["raw"].Value;

        return new ScimFilter(match.Groups["attr"].Value, value);
    }

    public bool Is(string attribute) => string.Equals(Attribute, attribute, StringComparison.OrdinalIgnoreCase);
}
