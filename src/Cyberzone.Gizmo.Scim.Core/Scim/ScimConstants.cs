namespace Cyberzone.Gizmo.Scim.Core.Scim;

public static class ScimConstants
{
    public const string MediaType = "application/scim+json";

    public const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string GroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string ListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    public const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";
    public const string PatchOpSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    public const string ServiceProviderConfigSchema = "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig";
    public const string ResourceTypeSchema = "urn:ietf:params:scim:schemas:core:2.0:ResourceType";
    public const string SchemaSchema = "urn:ietf:params:scim:schemas:core:2.0:Schema";

    /// <summary>
    /// Extension used to pass arbitrary Gizmo user attributes:
    /// { "urn:...:gizmo:2.0:User": { "attributes": { "Name": "Value" } } }
    /// </summary>
    public const string GizmoExtensionSchema = "urn:cyberzone:params:scim:schemas:extension:gizmo:2.0:User";
}
