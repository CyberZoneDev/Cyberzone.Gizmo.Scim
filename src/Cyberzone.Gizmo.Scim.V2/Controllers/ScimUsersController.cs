using System.Text.Json;
using System.Text.Json.Nodes;
using Cyberzone.Gizmo.Scim.Core.Scim;
using Cyberzone.Gizmo.Scim.Core.Sync;
using Cyberzone.Gizmo.Scim.V2.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerService;
using SharedLib;

namespace Cyberzone.Gizmo.Scim.V2.Controllers;

[AllowAnonymous]
[ScimAuthorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("scim/v2/Users")]
public sealed class ScimUsersController : ControllerBase
{
    private readonly Service _service;

    public ScimUsersController(Service service)
    {
        _service = service;
    }

    [HttpGet("")]
    public IActionResult List([FromQuery] string? filter, [FromQuery] int? startIndex, [FromQuery] int? count) =>
        Execute(users => users.List(filter, startIndex, count, BaseUrl));

    [HttpGet("{id}")]
    public IActionResult Get(string id) => Execute(users => users.Get(id, BaseUrl));

    [HttpPost("")]
    public async Task<IActionResult> Create() =>
        await ExecuteWithBody((users, body) => users.Create(body, BaseUrl));

    [HttpPut("{id}")]
    public async Task<IActionResult> Replace(string id) =>
        await ExecuteWithBody((users, body) => users.Replace(id, body, BaseUrl));

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id) =>
        await ExecuteWithBody((users, body) => users.Patch(id, body, BaseUrl));

    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => Execute(users => users.Delete(id));

    private string BaseUrl => ScimRequest.BaseUrl(HttpContext);

    private IActionResult Execute(Func<ScimUserService, ScimResponse> action)
    {
        try
        {
            var settings = ModuleSettings.Current!;
            var users = new ScimUserService(new GizmoV2UserStore(_service, settings), settings, Log);
            return ScimActionResult.From(action(users));
        }
        catch (Exception exception)
        {
            _service.LogAddError("[SCIM] Request failed: " + Request.Method + " " + Request.Path, exception,
                LogCategories.Generic);
            return ScimActionResult.From(ScimResponse.Error(500, null, exception.Message));
        }
    }

    private async Task<IActionResult> ExecuteWithBody(Func<ScimUserService, JsonObject, ScimResponse> action)
    {
        JsonObject body;
        try
        {
            body = await ScimRequest.ReadBodyAsync(Request);
        }
        catch (JsonException exception)
        {
            return ScimActionResult.From(ScimResponse.Error(400, "invalidSyntax", exception.Message));
        }

        return Execute(users => action(users, body));
    }

    private void Log(string message) =>
        _service.LogAdd("[SCIM] " + message, LogCategories.Generic, EventTypes.Information);
}
