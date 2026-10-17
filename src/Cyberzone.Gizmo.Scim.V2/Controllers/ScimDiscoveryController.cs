using Cyberzone.Gizmo.Scim.Core.Scim;
using Cyberzone.Gizmo.Scim.V2.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cyberzone.Gizmo.Scim.V2.Controllers;

[AllowAnonymous]
[ScimAuthorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("scim/v2")]
public sealed class ScimDiscoveryController : ControllerBase
{
    [HttpGet("ServiceProviderConfig")]
    public IActionResult ServiceProviderConfig() =>
        ScimActionResult.From(new ScimResponse(200,
            ScimDiscovery.ServiceProviderConfig(ScimRequest.BaseUrl(HttpContext))));

    [HttpGet("ResourceTypes")]
    public IActionResult ResourceTypes() =>
        ScimActionResult.From(new ScimResponse(200, ScimDiscovery.ResourceTypes(ScimRequest.BaseUrl(HttpContext))));

    [HttpGet("Schemas")]
    public IActionResult Schemas() => ScimActionResult.From(new ScimResponse(200, ScimDiscovery.Schemas()));
}
