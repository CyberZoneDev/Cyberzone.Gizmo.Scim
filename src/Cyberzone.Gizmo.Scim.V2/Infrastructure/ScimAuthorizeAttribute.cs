using Cyberzone.Gizmo.Scim.Core.Scim;
using Cyberzone.Gizmo.Scim.Core.Sync;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cyberzone.Gizmo.Scim.V2.Infrastructure;

/// <summary>
/// Checks the static bearer token from the plugin settings file. Gizmo's own Bearer scheme expects operator JWTs,
/// which Authentik cannot refresh, so the module authenticates SCIM calls itself.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ScimAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        ScimSettings? settings;
        try
        {
            settings = ModuleSettings.Current;
        }
        catch (Exception exception)
        {
            context.Result = ScimActionResult.From(ScimResponse.Error(500, null,
                ModuleSettings.FileName + " is invalid: " + exception.Message));
            return;
        }

        if (settings is null || string.IsNullOrWhiteSpace(settings.BearerToken))
        {
            context.Result = ScimActionResult.From(ScimResponse.Error(503, null,
                "SCIM plugin is not configured: set BearerToken in " + ModuleSettings.FileName + "."));
            return;
        }

        if (!BearerToken.IsValid(context.HttpContext.Request.Headers.Authorization, settings.BearerToken))
        {
            context.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
            context.Result = ScimActionResult.From(ScimResponse.Error(401, null, "Invalid or missing bearer token."));
        }
    }
}
