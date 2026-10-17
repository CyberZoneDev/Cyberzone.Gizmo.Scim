using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Cyberzone.Gizmo.Scim.Core.Scim;
using Cyberzone.Gizmo.Scim.V2.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cyberzone.Gizmo.Scim.V2.Controllers;

/// <summary>
/// Groups are not synchronized to Gizmo. These endpoints accept whatever Authentik sends
/// so the provider does not report errors or recreate groups on every sync.
/// </summary>
[AllowAnonymous]
[ScimAuthorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("scim/v2/Groups")]
public sealed class ScimGroupsController : ControllerBase
{
    [HttpGet("")]
    public IActionResult List() => ScimActionResult.From(ScimResponse.List(Array.Empty<JsonNode>(), 0, 1));

    [HttpGet("{id}")]
    public IActionResult Get(string id) => Echo(id, new JsonObject(), 200);

    [HttpPost("")]
    public async Task<IActionResult> Create()
    {
        var body = await ScimRequest.ReadBodyAsync(Request);
        var name = (body["displayName"] as JsonValue)?.GetValue<string>() ?? Guid.NewGuid().ToString("N");
        return Echo(StableId(name), body, 201);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Replace(string id) => Echo(id, await ScimRequest.ReadBodyAsync(Request), 200);

    [HttpPatch("{id}")]
    public IActionResult Patch(string id) => NoContent();

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => NoContent();

    private IActionResult Echo(string id, JsonObject body, int status)
    {
        var group = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.GroupSchema),
            ["id"] = id,
            ["displayName"] = (body["displayName"] as JsonValue)?.GetValue<string>() ?? id,
            ["members"] = new JsonArray(),
            ["meta"] = new JsonObject
            {
                ["resourceType"] = "Group",
                ["location"] = ScimRequest.BaseUrl(HttpContext) + "/Groups/" + id,
            },
        };

        return ScimActionResult.From(new ScimResponse(status, group));
    }

    private static string StableId(string name) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name))).Substring(0, 32).ToLowerInvariant();
}
