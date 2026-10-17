using System.Text.Json.Nodes;

namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// Static discovery documents (RFC 7643 section 5-7).
/// </summary>
public static class ScimDiscovery
{
    public static JsonObject ServiceProviderConfig(string baseUrl) => new()
    {
        ["schemas"] = new JsonArray(ScimConstants.ServiceProviderConfigSchema),
        ["patch"] = new JsonObject { ["supported"] = true },
        ["bulk"] = new JsonObject { ["supported"] = false, ["maxOperations"] = 0, ["maxPayloadSize"] = 0 },
        ["filter"] = new JsonObject { ["supported"] = true, ["maxResults"] = 200 },
        ["changePassword"] = new JsonObject { ["supported"] = false },
        ["sort"] = new JsonObject { ["supported"] = false },
        ["etag"] = new JsonObject { ["supported"] = false },
        ["authenticationSchemes"] = new JsonArray(new JsonObject
        {
            ["type"] = "oauthbearertoken",
            ["name"] = "Bearer token",
            ["description"] = "Static bearer token from the module settings",
        }),
        ["meta"] = new JsonObject
        {
            ["resourceType"] = "ServiceProviderConfig",
            ["location"] = baseUrl + "/ServiceProviderConfig",
        },
    };

    public static JsonObject ResourceTypes(string baseUrl) => new()
    {
        ["schemas"] = new JsonArray(ScimConstants.ListResponseSchema),
        ["totalResults"] = 2,
        ["Resources"] = new JsonArray(
            ResourceType("User", "/Users", ScimConstants.UserSchema, baseUrl, ScimConstants.GizmoExtensionSchema),
            ResourceType("Group", "/Groups", ScimConstants.GroupSchema, baseUrl, null)),
    };

    public static JsonObject Schemas() => new()
    {
        ["schemas"] = new JsonArray(ScimConstants.ListResponseSchema),
        ["totalResults"] = 2,
        ["Resources"] = new JsonArray(
            new JsonObject
            {
                ["schemas"] = new JsonArray(ScimConstants.SchemaSchema),
                ["id"] = ScimConstants.UserSchema,
                ["name"] = "User",
                ["attributes"] = new JsonArray(),
            },
            new JsonObject
            {
                ["schemas"] = new JsonArray(ScimConstants.SchemaSchema),
                ["id"] = ScimConstants.GizmoExtensionSchema,
                ["name"] = "GizmoUser",
                ["description"] = "Gizmo user attributes as { \"attributes\": { \"Name\": \"Value\" } }",
                ["attributes"] = new JsonArray(),
            }),
    };

    private static JsonObject ResourceType(string name, string endpoint, string schema, string baseUrl,
        string? extension)
    {
        var result = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.ResourceTypeSchema),
            ["id"] = name,
            ["name"] = name,
            ["endpoint"] = endpoint,
            ["schema"] = schema,
            ["meta"] = new JsonObject
            {
                ["resourceType"] = "ResourceType",
                ["location"] = baseUrl + "/ResourceTypes/" + name,
            },
        };

        if (extension is not null)
        {
            result["schemaExtensions"] = new JsonArray(new JsonObject { ["schema"] = extension, ["required"] = false });
        }

        return result;
    }
}
