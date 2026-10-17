using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace Cyberzone.Gizmo.Scim.V2.Infrastructure;

internal static class ScimRequest
{
    /// <summary>
    /// Reads the body as JSON regardless of Content-Type (application/scim+json is not a registered MVC format).
    /// </summary>
    public static async Task<JsonObject> ReadBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body);
        var text = await reader.ReadToEndAsync();
        return JsonNode.Parse(text) as JsonObject ?? throw new JsonException("Request body must be a JSON object.");
    }

    public static string BaseUrl(HttpContext context)
    {
        var configured = ModuleSettings.Current?.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!.TrimEnd('/');
        }

        var request = context.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}/scim/v2";
    }
}
