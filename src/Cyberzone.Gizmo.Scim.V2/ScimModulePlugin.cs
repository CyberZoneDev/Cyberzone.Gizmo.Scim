using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerService;
using SharedLib;
using GizmoServer = ServerService.Service;

namespace Cyberzone.Gizmo.Scim.V2;

/// <summary>
/// Gizmo service module plugin (plugins\ folder, enabled and scoped in Manager).
/// Registers this assembly's controllers in the server's own MVC, so SCIM is served
/// on the web portal port. Exported through [InheritedExport] on the base class.
/// </summary>
/// <remarks>
/// Works because the Gizmo service (and its module plugins) starts before the web host,
/// so the part is added before MVC builds its action descriptors.
/// </remarks>
public sealed class ScimModulePlugin : GizmoServiceModulePluginBase
{
    public override void Initialize()
    {
        var server = GizmoServer.Current;
        if (server is null)
        {
            return;
        }

        // Console/Serilog logger of the server, visible in the service output.
        var logger = server.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Cyberzone.Gizmo.Scim");
        logger?.LogInformation("[SCIM] Initialize started");

        try
        {
            var partManager = server.ServiceProvider.GetService<ApplicationPartManager>();
            logger?.LogInformation("[SCIM] ApplicationPartManager resolved: {Found}", partManager is not null);
            if (partManager is null)
            {
                server.LogAdd("[SCIM] ApplicationPartManager is not available, SCIM endpoint is not registered.",
                    LogCategories.Configuration, EventTypes.Error);
                return;
            }

            var assembly = typeof(ScimModulePlugin).Assembly;
            var name = assembly.GetName().Name;
            if (!partManager.ApplicationParts.Any(part =>
                    part is AssemblyPart assemblyPart && assemblyPart.Assembly.GetName().Name == name))
            {
                partManager.ApplicationParts.Add(new AssemblyPart(assembly));
            }

            logger?.LogInformation("[SCIM] Endpoint registered at /scim/v2");
            server.LogAdd("[SCIM] Endpoint registered at /scim/v2", LogCategories.Configuration,
                EventTypes.Information);
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "[SCIM] Could not register SCIM endpoint");
            server.LogAddError("[SCIM] Could not register SCIM endpoint.", exception, LogCategories.Configuration);
        }
    }
}
