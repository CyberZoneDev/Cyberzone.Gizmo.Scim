using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Cyberzone.Gizmo.Scim.Core.Scim;

namespace Cyberzone.Gizmo.Scim.Core.Sync;

/// <summary>
/// SCIM /Users semantics on top of <see cref="IGizmoUserStore"/>.
/// Link: SCIM id = Gizmo user id, SCIM externalId = Gizmo link attribute.
/// DELETE disables or soft-deletes the user (<see cref="ScimSettings.DeleteMode"/>). Groups are not synchronized.
/// </summary>
public sealed class ScimUserService
{
    private const int MaxPageSize = 200;

    // Writes are serialized so username/email uniqueness checks cannot race.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly IGizmoUserStore _store;
    private readonly ScimSettings _settings;
    private readonly Action<string>? _log;

    public ScimUserService(IGizmoUserStore store, ScimSettings settings, Action<string>? log = null)
    {
        _store = store;
        _settings = settings;
        _log = log;
    }

    public ScimResponse Get(string id, string baseUrl) => Run(() =>
    {
        var profile = FindById(id) ?? throw ScimException.NotFound($"User {id} not found.");
        return new ScimResponse(200, ToJson(profile, baseUrl));
    });

    public ScimResponse List(string? filter, int? startIndex, int? count, string baseUrl) => Run(() =>
    {
        var parsed = ScimFilter.Parse(filter);
        var start = Math.Max(1, startIndex ?? 1);
        var take = Math.Clamp(count ?? 100, 0, MaxPageSize);

        if (parsed is null)
        {
            var page = _store.ListManaged(start - 1, take, out var total);
            return ScimResponse.List(page.Select(x => (JsonNode)ToJson(x, baseUrl)), total, start);
        }

        var match = FindByFilter(parsed);
        var resources = match is null ? Array.Empty<JsonNode>() : new JsonNode[] { ToJson(match, baseUrl) };
        return ScimResponse.List(resources, resources.Length, 1);
    });

    public ScimResponse Create(JsonObject body, string baseUrl) => RunLocked(() =>
    {
        var user = ScimUser.FromJson(body);
        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            throw ScimException.BadRequest("invalidValue", "userName is required.");
        }

        if (string.IsNullOrWhiteSpace(user.ExternalId))
        {
            throw ScimException.BadRequest("invalidValue", "externalId is required to link the user.");
        }

        // Idempotent create: the same identity always maps to the same Gizmo user, even after a delete.
        var existing = _store.FindManagedByExternalId(user.ExternalId!) ?? Undelete(user.ExternalId!);
        if (existing is not null)
        {
            Apply(existing, user);
            return new ScimResponse(201, ToJson(_store.FindManagedById(existing.Id)!, baseUrl));
        }

        var profile = new GizmoUserProfile
        {
            ExternalId = user.ExternalId!.Trim(),
            Username = UsernameNormalizer.MakeUnique(user.UserName, name => _store.IsUsernameTaken(name, null)),
            Email = ResolveEmail(user.Email, null),
            FirstName = FieldLimits.Clip(FirstName(user), FieldLimits.FirstName),
            LastName = FieldLimits.Clip(user.FamilyName, FieldLimits.LastName),
            MobilePhone = FieldLimits.Clip(user.MobilePhone, FieldLimits.MobilePhone),
        };

        if (profile.Username != user.UserName)
        {
            Log($"Username '{user.UserName}' was changed to '{profile.Username}' to match Gizmo rules.");
        }

        profile.Id = _store.Create(profile);
        Log($"Created Gizmo user {profile.Id} '{profile.Username}' for externalId {profile.ExternalId}.");
        EnsurePassword(profile.Id, isNew: true);

        var attributes = AttributeChanges(user.Attributes, current: null);
        if (attributes.Count > 0)
        {
            _store.SetAttributes(profile.Id, attributes);
        }

        if (!user.Active)
        {
            _store.SetDisabled(profile.Id, true);
        }

        return new ScimResponse(201, ToJson(_store.FindManagedById(profile.Id)!, baseUrl));
    });

    public ScimResponse Replace(string id, JsonObject body, string baseUrl) => RunLocked(() =>
    {
        var existing = FindById(id) ?? throw ScimException.NotFound($"User {id} not found.");
        Apply(existing, ScimUser.FromJson(body));
        return new ScimResponse(200, ToJson(_store.FindManagedById(existing.Id)!, baseUrl));
    });

    public ScimResponse Patch(string id, JsonObject patch, string baseUrl) => RunLocked(() =>
    {
        var existing = FindById(id) ?? throw ScimException.NotFound($"User {id} not found.");
        var resource = ToJson(existing, baseUrl);
        ScimPatch.Apply(resource, patch);
        Apply(existing, ScimUser.FromJson(resource));
        return new ScimResponse(200, ToJson(_store.FindManagedById(existing.Id)!, baseUrl));
    });

    /// <summary>
    /// DELETE disables the user or, with <see cref="DeleteMode.Delete"/>, soft-deletes it.
    /// </summary>
    public ScimResponse Delete(string id) => RunLocked(() =>
    {
        var existing = FindById(id) ?? throw ScimException.NotFound($"User {id} not found.");
        if (_settings.DeleteMode == DeleteMode.Delete)
        {
            _store.Delete(existing.Id);
            Log($"Deleted Gizmo user {existing.Id} '{existing.Username}' (deleted in identity provider).");
        }
        else if (!existing.IsDisabled)
        {
            _store.SetDisabled(existing.Id, true);
            Log($"Disabled Gizmo user {existing.Id} '{existing.Username}' (deleted in identity provider).");
        }

        return new ScimResponse(204, null);
    });

    public static JsonObject ToJson(GizmoUserProfile profile, string baseUrl)
    {
        var id = profile.Id.ToString(CultureInfo.InvariantCulture);
        var json = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.UserSchema, ScimConstants.GizmoExtensionSchema),
            ["id"] = id,
            ["externalId"] = profile.ExternalId,
            ["userName"] = profile.Username,
            ["displayName"] = DisplayName(profile),
            ["active"] = !profile.IsDisabled,
        };

        if (profile.FirstName is not null || profile.LastName is not null)
        {
            json["name"] = new JsonObject
            {
                ["givenName"] = profile.FirstName,
                ["familyName"] = profile.LastName,
            };
        }

        if (!string.IsNullOrEmpty(profile.Email))
        {
            json["emails"] = new JsonArray(new JsonObject
            {
                ["value"] = profile.Email,
                ["type"] = "work",
                ["primary"] = true,
            });
        }

        if (!string.IsNullOrEmpty(profile.MobilePhone))
        {
            json["phoneNumbers"] = new JsonArray(new JsonObject
            {
                ["value"] = profile.MobilePhone,
                ["type"] = "mobile",
            });
        }

        var attributes = new JsonObject();
        foreach (var pair in profile.Attributes.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            attributes[pair.Key] = pair.Value;
        }

        json[ScimConstants.GizmoExtensionSchema] = new JsonObject { ["attributes"] = attributes };

        var meta = new JsonObject
        {
            ["resourceType"] = "User",
            ["location"] = baseUrl + "/Users/" + id,
        };
        if (profile.CreatedTime is { } created)
        {
            meta["created"] = created.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        if ((profile.ModifiedTime ?? profile.CreatedTime) is { } modified)
        {
            meta["lastModified"] = modified.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        json["meta"] = meta;
        return json;
    }

    private void Apply(GizmoUserProfile current, ScimUser user)
    {
        var updated = new GizmoUserProfile
        {
            Id = current.Id,
            ExternalId = current.ExternalId,
            Username = ResolveUsername(current, user.UserName),
            Email = ResolveEmail(user.Email, current),
            FirstName = FieldLimits.Clip(FirstName(user), FieldLimits.FirstName),
            LastName = FieldLimits.Clip(user.FamilyName, FieldLimits.LastName),
            MobilePhone = FieldLimits.Clip(user.MobilePhone, FieldLimits.MobilePhone),
            IsDisabled = current.IsDisabled,
        };

        if (updated.Username != current.Username
            || !string.Equals(updated.Email, current.Email, StringComparison.OrdinalIgnoreCase)
            || updated.FirstName != current.FirstName
            || updated.LastName != current.LastName
            || updated.MobilePhone != current.MobilePhone)
        {
            _store.UpdateProfile(updated);
            if (updated.Username != current.Username)
            {
                Log($"Renamed Gizmo user {current.Id} from '{current.Username}' to '{updated.Username}'.");
            }
        }

        var attributes = AttributeChanges(user.Attributes, current.Attributes);
        if (attributes.Count > 0)
        {
            _store.SetAttributes(current.Id, attributes);
        }

        EnsurePassword(current.Id, isNew: false);

        var disabled = !user.Active;
        if (disabled != current.IsDisabled)
        {
            _store.SetDisabled(current.Id, disabled);
            Log($"{(disabled ? "Disabled" : "Enabled")} Gizmo user {current.Id} '{updated.Username}'.");
        }
    }

    // Gizmo accepts any password for a customer without one, so synced users get a random password nobody knows.
    // A password already set (e.g. by an operator) is kept.
    private void EnsurePassword(int id, bool isNew)
    {
        if (!_settings.SetRandomPassword || (!isNew && _store.HasPassword(id)))
        {
            return;
        }

        _store.SetPassword(id, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        if (!isNew)
        {
            Log($"Set a random password for Gizmo user {id}, it had none.");
        }
    }

    private string ResolveUsername(GizmoUserProfile current, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return current.Username;
        }

        var normalized = UsernameNormalizer.Normalize(requested);

        // Keep "name_2" when the identity provider still sends "name": the suffix came from a collision.
        if (string.Equals(current.Username, normalized, StringComparison.OrdinalIgnoreCase)
            || IsSuffixedVariant(current.Username, normalized))
        {
            return current.Username;
        }

        var unique = UsernameNormalizer.MakeUnique(requested, name => _store.IsUsernameTaken(name, current.Id));
        if (unique != requested)
        {
            Log($"Username '{requested}' was changed to '{unique}' to match Gizmo rules.");
        }

        return unique;
    }

    private static bool IsSuffixedVariant(string username, string normalized)
    {
        var match = Regex.Match(username, @"^(?<base>.*)_(?<n>\d+)$");
        if (!match.Success)
        {
            return false;
        }

        var baseName = match.Groups["base"].Value;
        return string.Equals(baseName, normalized, StringComparison.OrdinalIgnoreCase)
               || (normalized.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                   && username.Length == UsernameNormalizer.MaxLength);
    }

    private string? ResolveEmail(string? requested, GizmoUserProfile? current)
    {
        var email = FieldLimits.Clip(requested, FieldLimits.Email);
        if (email is null)
        {
            return null;
        }

        if (current is not null && string.Equals(current.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return current.Email;
        }

        if (_store.IsEmailTaken(email, current?.Id))
        {
            if (_settings.RejectDuplicateEmail)
            {
                throw new ScimException(409, "uniqueness", $"Email '{email}' already belongs to another Gizmo user.");
            }

            Log($"Email '{email}' already belongs to another Gizmo user, left empty.");
            return current?.Email is { } kept && !_store.IsEmailTaken(kept, current.Id) ? kept : null;
        }

        return email;
    }

    private Dictionary<string, string?> AttributeChanges(Dictionary<string, string?>? requested,
        Dictionary<string, string>? current)
    {
        var changes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (requested is null)
        {
            return changes;
        }

        var allowed = new HashSet<string>(_settings.AllowedAttributes, StringComparer.OrdinalIgnoreCase);
        allowed.Remove(_settings.LinkAttributeName);

        foreach (var name in allowed)
        {
            requested.TryGetValue(name, out var value);
            value = FieldLimits.Clip(value, FieldLimits.AttributeValue);

            string? existing = null;
            current?.TryGetValue(name, out existing);

            if (!string.Equals(value, existing, StringComparison.Ordinal))
            {
                // Attribute missing from the request means "remove" only when the extension was sent at all.
                changes[name] = value;
            }
        }

        foreach (var name in requested.Keys.Where(x => !allowed.Contains(x)))
        {
            Log($"Attribute '{name}' is not in AllowedAttributes, ignored.");
        }

        return changes;
    }

    private GizmoUserProfile? Undelete(string externalId)
    {
        if (_store.FindDeletedIdByExternalId(externalId) is not { } id)
        {
            return null;
        }

        _store.Undelete(id);
        var profile = _store.FindManagedById(id);
        Log($"Restored deleted Gizmo user {id} '{profile?.Username}' for externalId {externalId}.");
        return profile;
    }

    private GizmoUserProfile? FindById(string id) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            ? _store.FindManagedById(userId)
            : null;

    private GizmoUserProfile? FindByFilter(ScimFilter filter)
    {
        if (filter.Is("id"))
        {
            return FindById(filter.Value);
        }

        if (filter.Is("externalId"))
        {
            return _store.FindManagedByExternalId(filter.Value);
        }

        if (filter.Is("userName"))
        {
            return _store.FindManagedByUsername(filter.Value)
                   ?? _store.FindManagedByUsername(UsernameNormalizer.Normalize(filter.Value));
        }

        throw ScimException.BadRequest("invalidFilter", $"Filtering by '{filter.Attribute}' is not supported.");
    }

    private static string? FirstName(ScimUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.GivenName))
        {
            return user.GivenName;
        }

        // Authentik's default mapping only has a full name; use it when no structured name is sent.
        return string.IsNullOrWhiteSpace(user.FamilyName) ? user.DisplayName : null;
    }

    private static string? DisplayName(GizmoUserProfile profile)
    {
        var name = string.Join(" ",
            new[] { profile.FirstName, profile.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return name.Length > 0 ? name : profile.Username;
    }

    private void Log(string message) => _log?.Invoke(message);

    private static ScimResponse Run(Func<ScimResponse> action)
    {
        try
        {
            return action();
        }
        catch (ScimException exception)
        {
            return ScimResponse.FromException(exception);
        }
    }

    private static ScimResponse RunLocked(Func<ScimResponse> action)
    {
        WriteLock.Wait();
        try
        {
            return Run(action);
        }
        finally
        {
            WriteLock.Release();
        }
    }
}
