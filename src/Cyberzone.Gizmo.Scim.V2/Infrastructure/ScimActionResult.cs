using Cyberzone.Gizmo.Scim.Core.Scim;
using Microsoft.AspNetCore.Mvc;

namespace Cyberzone.Gizmo.Scim.V2.Infrastructure;

/// <summary>
/// Writes SCIM JSON directly, bypassing Gizmo's MVC formatters (MessagePack, response wrapper).
/// </summary>
internal static class ScimActionResult
{
    public static IActionResult From(ScimResponse response)
    {
        if (response.Body is null)
        {
            return new StatusCodeResult(response.Status);
        }

        return new ContentResult
        {
            StatusCode = response.Status,
            ContentType = ScimConstants.MediaType + "; charset=utf-8",
            Content = response.Body.ToJsonString(),
        };
    }
}
