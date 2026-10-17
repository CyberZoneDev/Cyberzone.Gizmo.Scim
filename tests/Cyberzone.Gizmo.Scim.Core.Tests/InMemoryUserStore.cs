using Cyberzone.Gizmo.Scim.Core.Sync;

namespace Cyberzone.Gizmo.Scim.Core.Tests;

/// <summary>Fake Gizmo store. Unmanaged users have no ExternalId.</summary>
internal sealed class InMemoryUserStore : IGizmoUserStore
{
    private int _nextId = 1;

    public List<GizmoUserProfile> Users { get; } = new();

    public int UpdateCalls { get; private set; }

    public GizmoUserProfile AddUnmanaged(string username, string? email = null)
    {
        var user = new GizmoUserProfile { Id = _nextId++, Username = username, Email = email };
        Users.Add(user);
        return user;
    }

    public GizmoUserProfile? FindManagedById(int id) =>
        Copy(Users.SingleOrDefault(x => x.Id == id && x.ExternalId != null));

    public GizmoUserProfile? FindManagedByExternalId(string externalId) =>
        Copy(Users.SingleOrDefault(x => x.ExternalId == externalId));

    public GizmoUserProfile? FindManagedByUsername(string username) =>
        Copy(Users.SingleOrDefault(x =>
            x.ExternalId != null && string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase)));

    public IReadOnlyList<GizmoUserProfile> ListManaged(int skip, int take, out int total)
    {
        var managed = Users.Where(x => x.ExternalId != null).OrderBy(x => x.Id).ToList();
        total = managed.Count;
        return managed.Skip(skip).Take(take).Select(x => Copy(x)!).ToList();
    }

    public bool IsUsernameTaken(string username, int? exceptUserId) =>
        Users.Any(x => x.Id != exceptUserId && string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));

    public bool IsEmailTaken(string email, int? exceptUserId) =>
        Users.Any(x => x.Id != exceptUserId && string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));

    public int Create(GizmoUserProfile profile)
    {
        var user = Copy(profile)!;
        user.Id = _nextId++;
        Users.Add(user);
        return user.Id;
    }

    public void UpdateProfile(GizmoUserProfile profile)
    {
        UpdateCalls++;
        var user = Users.Single(x => x.Id == profile.Id);
        user.Username = profile.Username;
        user.Email = profile.Email;
        user.FirstName = profile.FirstName;
        user.LastName = profile.LastName;
        user.MobilePhone = profile.MobilePhone;
    }

    public void SetDisabled(int id, bool disabled) => Users.Single(x => x.Id == id).IsDisabled = disabled;

    public void SetAttributes(int id, IReadOnlyDictionary<string, string?> attributes)
    {
        var user = Users.Single(x => x.Id == id);
        foreach (var pair in attributes)
        {
            if (pair.Value is null)
            {
                user.Attributes.Remove(pair.Key);
            }
            else
            {
                user.Attributes[pair.Key] = pair.Value;
            }
        }
    }

    private static GizmoUserProfile? Copy(GizmoUserProfile? x) => x is null
        ? null
        : new GizmoUserProfile
        {
            Id = x.Id,
            ExternalId = x.ExternalId,
            Username = x.Username,
            Email = x.Email,
            FirstName = x.FirstName,
            LastName = x.LastName,
            MobilePhone = x.MobilePhone,
            IsDisabled = x.IsDisabled,
            Attributes = new Dictionary<string, string>(x.Attributes, StringComparer.OrdinalIgnoreCase),
        };
}
