using System.Globalization;
using System.Text.Json.Nodes;

namespace Cyberzone.Gizmo.Scim.Core.Scim;

/// <summary>
/// Transport-independent HTTP answer: status code plus optional SCIM JSON body.
/// </summary>
public sealed class ScimResponse
{
    public ScimResponse(int status, JsonNode? body)
    {
        Status = status;
        Body = body;
    }

    public int Status { get; }

    public JsonNode? Body { get; }

    public static ScimResponse Error(int status, string? scimType, string detail)
    {
        var body = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.ErrorSchema),
            ["status"] = status.ToString(CultureInfo.InvariantCulture),
            ["detail"] = detail,
        };

        if (!string.IsNullOrEmpty(scimType))
        {
            body["scimType"] = scimType;
        }

        return new ScimResponse(status, body);
    }

    public static ScimResponse FromException(ScimException exception) =>
        Error(exception.Status, exception.ScimType, exception.Message);

    public static ScimResponse List(IEnumerable<JsonNode> resources, int total, int startIndex)
    {
        var array = new JsonArray();
        foreach (var resource in resources)
        {
            array.Add(resource);
        }

        return new ScimResponse(200, new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.ListResponseSchema),
            ["totalResults"] = total,
            ["startIndex"] = startIndex,
            ["itemsPerPage"] = array.Count,
            ["Resources"] = array,
        });
    }
}
