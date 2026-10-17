using System.Collections.Concurrent;
using Cyberzone.Gizmo.Scim.Core.Sync;
using Gizmo.DAL.Entities;
using ServerService;
using SharedLib;
using GizmoAttribute = Gizmo.DAL.Entities.Attribute;
using GizmoDbContext = Gizmo.DAL.Contexts.DefaultDbContext;

namespace Cyberzone.Gizmo.Scim.V2;

/// <summary>
/// Gizmo v2 implementation. Reads go straight to the database; writes go through the same
/// Service methods the operator console uses (UserCreate, UserUpdate, UserDisable, UserAttribute*),
/// so renames move the user profile folder and connected clients get change events.
/// </summary>
internal sealed class GizmoV2UserStore : IGizmoUserStore
{
    // Attribute definition ids by name; attributes are created once on first use.
    private static readonly ConcurrentDictionary<string, int> AttributeIds = new(StringComparer.OrdinalIgnoreCase);

    private readonly Service _service;
    private readonly string _linkAttribute;

    public GizmoV2UserStore(Service service, ScimSettings settings)
    {
        _service = service;
        _linkAttribute = settings.LinkAttributeName;
    }

    public GizmoUserProfile? FindManagedById(int id)
    {
        using var cx = _service.GetDbNonProxyContext();
        var linkId = AttributeId(_linkAttribute);
        var user = ManagedUsers(cx, linkId).SingleOrDefault(x => x.Id == id);
        return user is null ? null : ToProfile(cx, user, linkId);
    }

    public GizmoUserProfile? FindManagedByExternalId(string externalId)
    {
        using var cx = _service.GetDbNonProxyContext();
        var linkId = AttributeId(_linkAttribute);
        var userId = cx.UserAttribute.AsNoTracking()
            .Where(x => x.AttributeId == linkId && x.Value == externalId)
            .Select(x => x.UserId)
            .FirstOrDefault();
        if (userId == 0)
        {
            return null;
        }

        var user = ManagedUsers(cx, linkId).SingleOrDefault(x => x.Id == userId);
        return user is null ? null : ToProfile(cx, user, linkId);
    }

    public GizmoUserProfile? FindManagedByUsername(string username)
    {
        using var cx = _service.GetDbNonProxyContext();
        var linkId = AttributeId(_linkAttribute);
        var lower = username.ToLower();
        var user = ManagedUsers(cx, linkId).FirstOrDefault(x => x.Username.ToLower() == lower);
        return user is null ? null : ToProfile(cx, user, linkId);
    }

    public IReadOnlyList<GizmoUserProfile> ListManaged(int skip, int take, out int total)
    {
        using var cx = _service.GetDbNonProxyContext();
        var linkId = AttributeId(_linkAttribute);
        var query = ManagedUsers(cx, linkId);
        total = query.Count();
        var page = query.OrderBy(x => x.Id).Skip(skip).Take(take).ToList();
        return page.Select(x => ToProfile(cx, x, linkId)).ToList();
    }

    public bool IsUsernameTaken(string username, int? exceptUserId)
    {
        using var cx = _service.GetDbNonProxyContext();
        var lower = username.ToLower();
        var except = exceptUserId ?? 0;

        // Deleted users still own their username (unique index UQ_Username).
        return cx.UsersMember.AsNoTracking().Any(x => x.Id != except && x.Username.ToLower() == lower);
    }

    public bool IsEmailTaken(string email, int? exceptUserId)
    {
        using var cx = _service.GetDbNonProxyContext();
        var lower = email.ToLower();
        var except = exceptUserId ?? 0;
        return cx.UsersMember.AsNoTracking().Any(x => x.Id != except && x.Email != null && x.Email.ToLower() == lower);
    }

    public int Create(GizmoUserProfile profile)
    {
        var user = new UserMember
        {
            Username = profile.Username,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            MobilePhone = profile.MobilePhone,
            UserGroupId = DefaultUserGroupId(),
        };

        // Same path as the operator console: inserts the user with an empty credential and raises the "added" event.
        _service.UserCreate(user);

        _service.UserAttributeSetValue(user.Id, AttributeId(_linkAttribute), profile.ExternalId!);
        return user.Id;
    }

    public void UpdateProfile(GizmoUserProfile profile)
    {
        UserMember current;
        using (var cx = _service.GetDbNonProxyContext())
        {
            current = cx.UsersMember.AsNoTracking().Single(x => x.Id == profile.Id);
        }

        // Service.UserUpdate writes every profile column,
        // so start from the stored values and change only the synchronized fields.
        // Group, billing, disabled state and smart card are protected inside UserUpdate itself.
        var user = new UserMember
        {
            Id = current.Id,
            Guid = current.Guid,
            UserGroupId = current.UserGroupId,
            IsNegativeBalanceAllowed = current.IsNegativeBalanceAllowed,
            IsPersonalInfoRequested = current.IsPersonalInfoRequested,
            BillingOptions = current.BillingOptions,
            EnableDate = current.EnableDate,
            DisabledDate = current.DisabledDate,
            BirthDate = current.BirthDate,
            Address = current.Address,
            City = current.City,
            Country = current.Country,
            PostCode = current.PostCode,
            Phone = current.Phone,
            Sex = current.Sex,
            IsDeleted = current.IsDeleted,
            IsDisabled = current.IsDisabled,
            SmartCardUID = current.SmartCardUID,
            Identification = current.Identification,
            CreatedById = current.CreatedById,
            CreatedTime = current.CreatedTime,

            Username = profile.Username,
            Email = profile.Email,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            MobilePhone = profile.MobilePhone,
        };

        _service.UserUpdate(user);
    }

    public void SetDisabled(int id, bool disabled) => _service.UserDisable(id, disabled);

    public void SetAttributes(int id, IReadOnlyDictionary<string, string?> attributes)
    {
        foreach (var pair in attributes)
        {
            var attributeId = AttributeId(pair.Key);
            if (pair.Value is null)
            {
                _service.UserAttributeRemove(id, attributeId);
            }
            else
            {
                _service.UserAttributeSetValue(id, attributeId, pair.Value);
            }
        }
    }

    private static IQueryable<UserMember> ManagedUsers(GizmoDbContext cx, int linkId) =>
        cx.UsersMember.AsNoTracking().Where(x =>
            !x.IsDeleted && cx.UserAttribute.Any(a => a.UserId == x.Id && a.AttributeId == linkId));

    private static GizmoUserProfile ToProfile(GizmoDbContext cx, UserMember user, int linkId)
    {
        var attributes = (from ua in cx.UserAttribute.AsNoTracking()
            join a in cx.Attributes.AsNoTracking() on ua.AttributeId equals a.Id
            where ua.UserId == user.Id
            select new { ua.AttributeId, a.Name, ua.Value }).ToList();

        var profile = new GizmoUserProfile
        {
            Id = user.Id,
            ExternalId = attributes.FirstOrDefault(x => x.AttributeId == linkId)?.Value,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MobilePhone = user.MobilePhone,
            IsDisabled = user.IsDisabled,
            CreatedTime = user.CreatedTime,
            ModifiedTime = user.ModifiedTime,
        };

        foreach (var attribute in attributes.Where(x => x.AttributeId != linkId && x.Value != null))
        {
            profile.Attributes[attribute.Name] = attribute.Value;
        }

        return profile;
    }

    private int AttributeId(string name) => AttributeIds.GetOrAdd(name, key =>
    {
        using (var cx = _service.GetDbNonProxyContext())
        {
            var existing = cx.Attributes.AsNoTracking().Where(x => x.Name == key).Select(x => (int?)x.Id)
                .FirstOrDefault();
            if (existing is { } id)
            {
                return id;
            }
        }

        var attribute = new GizmoAttribute { Name = key, FriendlyName = key };
        _service.AttributeAdd(attribute);
        _service.LogAdd($"[SCIM] Created Gizmo user attribute '{key}'.", LogCategories.Generic, EventTypes.Information);
        return attribute.Id;
    });

    private int DefaultUserGroupId()
    {
        using var cx = _service.GetDbNonProxyContext();
        var groupId = cx.UserGroups.AsNoTracking().Where(x => x.IsDefault).Select(x => (int?)x.Id).FirstOrDefault();
        return groupId ??
               throw new InvalidOperationException("Gizmo has no default user group. Mark one user group as default.");
    }
}
