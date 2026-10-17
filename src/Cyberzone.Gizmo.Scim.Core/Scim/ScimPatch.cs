using System.Text.Json.Nodes;

namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// Applies a SCIM PatchOp to a resource JSON. Supports add/replace/remove with no path,
/// simple paths ("active", "name.givenName") and the "emails[type eq \"work\"].value" style that some clients use.
/// </summary>
public static class ScimPatch
{
    public static void Apply(JsonObject resource, JsonObject patch)
    {
        if (ScimUser.GetIgnoreCase(patch, "Operations") is not JsonArray operations)
        {
            throw ScimException.BadRequest("invalidSyntax", "PatchOp must contain Operations.");
        }

        foreach (var operation in operations.OfType<JsonObject>())
        {
            var op = (ScimUser.GetIgnoreCase(operation, "op") as JsonValue)?.GetValue<string>()?.ToLowerInvariant();
            var path = (ScimUser.GetIgnoreCase(operation, "path") as JsonValue)?.GetValue<string>();
            var value = ScimUser.GetIgnoreCase(operation, "value");

            switch (op)
            {
                case "add":
                case "replace":
                    if (string.IsNullOrEmpty(path))
                    {
                        if (value is not JsonObject values)
                        {
                            throw ScimException.BadRequest("invalidValue",
                                "Operation without path needs an object value.");
                        }

                        foreach (var pair in values.ToList())
                        {
                            SetPath(resource, pair.Key, Copy(pair.Value));
                        }
                    }
                    else
                    {
                        SetPath(resource, path!, Copy(value));
                    }

                    break;

                case "remove":
                    if (string.IsNullOrEmpty(path))
                    {
                        throw ScimException.BadRequest("noTarget", "Remove operation needs a path.");
                    }

                    SetPath(resource, path!, null);
                    break;

                default:
                    throw ScimException.BadRequest("invalidSyntax", $"Unsupported patch op '{op}'.");
            }
        }
    }

    private static void SetPath(JsonObject resource, string path, JsonNode? value)
    {
        // Extension attributes: "urn:...:User:attributes.Name" or the whole extension object.
        if (path.StartsWith(ScimConstants.GizmoExtensionSchema, StringComparison.OrdinalIgnoreCase))
        {
            var rest = path.Substring(ScimConstants.GizmoExtensionSchema.Length).TrimStart(':', '.');
            var extension = GetOrCreateObject(resource, ScimConstants.GizmoExtensionSchema);
            if (rest.Length == 0)
            {
                if (value is JsonObject obj)
                {
                    foreach (var pair in obj.ToList())
                    {
                        extension[pair.Key] = Copy(pair.Value);
                    }
                }

                return;
            }

            SetDotted(extension, rest, value);
            return;
        }

        var filterStart = path.IndexOf('[');
        if (filterStart > 0)
        {
            SetMultiValued(resource, path, filterStart, value);
            return;
        }

        SetDotted(resource, path, value);
    }

    private static void SetDotted(JsonObject target, string path, JsonNode? value)
    {
        var parts = path.Split('.');
        var current = target;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            current = GetOrCreateObject(current, parts[i]);
        }

        var key = FindKey(current, parts[^1]) ?? parts[^1];
        if (value is null)
        {
            current.Remove(key);
        }
        else
        {
            current[key] = value;
        }
    }

    // Handles "emails[type eq \"work\"].value" and "emails[primary eq true].value".
    private static void SetMultiValued(JsonObject resource, string path, int filterStart, JsonNode? value)
    {
        var filterEnd = path.IndexOf(']', filterStart);
        if (filterEnd < 0)
        {
            throw ScimException.BadRequest("invalidPath", $"Invalid path '{path}'.");
        }

        var attribute = path.Substring(0, filterStart);
        var filter = ScimFilter.Parse(path.Substring(filterStart + 1, filterEnd - filterStart - 1))!;
        var subAttribute = filterEnd + 1 < path.Length ? path.Substring(filterEnd + 1).TrimStart('.') : null;

        var key = FindKey(resource, attribute) ?? attribute;
        if (resource[key] is not JsonArray array)
        {
            array = new JsonArray();
            resource[key] = array;
        }

        var item = array.OfType<JsonObject>().FirstOrDefault(x =>
            string.Equals(ValueToString(ScimUser.GetIgnoreCase(x, filter.Attribute)), filter.Value,
                StringComparison.OrdinalIgnoreCase));

        if (value is null)
        {
            if (item is not null)
            {
                if (string.IsNullOrEmpty(subAttribute))
                {
                    array.Remove(item);
                }
                else
                {
                    item.Remove(FindKey(item, subAttribute!) ?? subAttribute!);
                }
            }

            return;
        }

        if (item is null)
        {
            item = new JsonObject { [filter.Attribute] = filter.Value };
            array.Add(item);
        }

        if (string.IsNullOrEmpty(subAttribute))
        {
            if (value is JsonObject obj)
            {
                foreach (var pair in obj.ToList())
                {
                    item[pair.Key] = Copy(pair.Value);
                }
            }
        }
        else
        {
            item[FindKey(item, subAttribute!) ?? subAttribute!] = value;
        }
    }

    // JsonNode.DeepClone exists only since .NET 8 but the v2 plugin targets .NET 6. This is a workaround!
    private static JsonNode? Copy(JsonNode? node) => node is null ? null : JsonNode.Parse(node.ToJsonString());

    private static JsonObject GetOrCreateObject(JsonObject parent, string name)
    {
        var key = FindKey(parent, name) ?? name;
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var created = new JsonObject();
        parent[key] = created;
        return created;
    }

    private static string? FindKey(JsonObject json, string name) =>
        json.Select(x => x.Key).FirstOrDefault(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

    private static string? ValueToString(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => node.ToJsonString(),
    };
}
