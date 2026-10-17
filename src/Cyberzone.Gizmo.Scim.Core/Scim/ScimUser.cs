using System.Text.Json.Nodes;

namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// The subset of a SCIM User resource that is synchronized to Gizmo.
/// </summary>
public sealed class ScimUser
{
    public string? ExternalId { get; set; }

    public string? UserName { get; set; }

    public string? GivenName { get; set; }

    public string? FamilyName { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? MobilePhone { get; set; }

    public bool Active { get; set; } = true;

    /// <summary>
    /// Gizmo attributes from the extension schema. Null value means "remove".
    /// Null dictionary means the extension was not sent and attributes stay untouched.
    /// </summary>
    public Dictionary<string, string?>? Attributes { get; set; }

    public static ScimUser FromJson(JsonObject json)
    {
        var user = new ScimUser
        {
            ExternalId = GetString(json, "externalId"),
            UserName = GetString(json, "userName"),
            DisplayName = GetString(json, "displayName"),
            Active = GetBool(json, "active") ?? true,
        };

        if (GetIgnoreCase(json, "name") is JsonObject name)
        {
            user.GivenName = GetString(name, "givenName");
            user.FamilyName = GetString(name, "familyName");
        }

        user.Email = PickMultiValued(GetIgnoreCase(json, "emails") as JsonArray, preferredType: "work");
        user.MobilePhone = PickMultiValued(GetIgnoreCase(json, "phoneNumbers") as JsonArray, preferredType: "mobile");

        if (GetIgnoreCase(json, ScimConstants.GizmoExtensionSchema) is JsonObject extension
            && GetIgnoreCase(extension, "attributes") is JsonObject attributes)
        {
            user.Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in attributes)
            {
                user.Attributes[pair.Key] = pair.Value is null ? null : NodeToString(pair.Value);
            }
        }

        return user;
    }

    internal static JsonNode? GetIgnoreCase(JsonObject json, string name)
    {
        if (json.TryGetPropertyValue(name, out var exact))
        {
            return exact;
        }

        foreach (var pair in json)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? GetString(JsonObject json, string name)
    {
        var node = GetIgnoreCase(json, name);
        return node is null ? null : NodeToString(node);
    }

    private static bool? GetBool(JsonObject json, string name)
    {
        var node = GetIgnoreCase(json, name);
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var b))
            {
                return b;
            }

            // Some clients send "True"/"False" as strings.
            if (value.TryGetValue<string>(out var s) && bool.TryParse(s, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string? NodeToString(JsonNode node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var s))
            {
                return s;
            }

            return value.ToJsonString();
        }

        return node.ToJsonString();
    }

    private static string? PickMultiValued(JsonArray? array, string preferredType)
    {
        if (array is null || array.Count == 0)
        {
            return null;
        }

        var items = array.OfType<JsonObject>().ToList();

        var chosen = items.FirstOrDefault(x => GetBool(x, "primary") == true)
                     ?? items.FirstOrDefault(x =>
                         string.Equals(GetString(x, "type"), preferredType, StringComparison.OrdinalIgnoreCase))
                     ?? items.FirstOrDefault();

        var result = chosen is null ? null : GetString(chosen, "value");
        return string.IsNullOrWhiteSpace(result) ? null : result!.Trim();
    }
}
