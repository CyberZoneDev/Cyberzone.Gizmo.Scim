using System.Text.Json.Nodes;
using Cyberzone.Gizmo.Scim.Core.Scim;
using Cyberzone.Gizmo.Scim.Core.Sync;

namespace Cyberzone.Gizmo.Scim.Core.Tests;

public class ScimUserServiceTests
{
    private const string BaseUrl = "https://gizmo.example/scim/v2";

    private readonly InMemoryUserStore _store = new();
    private readonly ScimUserService _service;

    public ScimUserServiceTests()
    {
        _service = new ScimUserService(_store, new ScimSettings { AllowedAttributes = { "Level" } });
    }

    [Fact]
    public void Create_links_user_and_returns_gizmo_id()
    {
        var response = _service.Create(User("ak-1", "john"), BaseUrl);

        Assert.Equal(201, response.Status);
        var created = _store.Users.Single();
        Assert.Equal("ak-1", created.ExternalId);
        Assert.Equal(created.Id.ToString(), response.Body!["id"]!.GetValue<string>());
    }

    [Fact]
    public void Create_is_idempotent_by_external_id()
    {
        _service.Create(User("ak-1", "john"), BaseUrl);
        var second = _service.Create(User("ak-1", "john2"), BaseUrl);

        Assert.Equal(201, second.Status);
        var user = Assert.Single(_store.Users);
        Assert.Equal("john2", user.Username);
    }

    [Fact]
    public void Create_requires_external_id()
    {
        var body = User("ak-1", "john");
        body.Remove("externalId");

        Assert.Equal(400, _service.Create(body, BaseUrl).Status);
    }

    [Fact]
    public void Create_does_not_take_over_existing_club_user()
    {
        var clubUser = _store.AddUnmanaged("john", "john@example.com");

        _service.Create(User("ak-1", "john", email: "john@example.com"), BaseUrl);

        var created = _store.Users.Single(x => x.ExternalId == "ak-1");
        Assert.NotEqual(clubUser.Id, created.Id);
        Assert.Equal("john_2", created.Username);
        Assert.Null(created.Email);
    }

    [Fact]
    public void Create_inactive_user_is_disabled()
    {
        _service.Create(User("ak-1", "john", active: false), BaseUrl);
        Assert.True(_store.Users.Single().IsDisabled);
    }

    [Fact]
    public void Create_sets_random_password()
    {
        _service.Create(User("ak-1", "john"), BaseUrl);
        _service.Create(User("ak-2", "jane"), BaseUrl);

        Assert.Equal(2, _store.Passwords.Count);
        Assert.NotEqual(_store.Passwords.Values.First(), _store.Passwords.Values.Last());
        Assert.All(_store.Passwords.Values, x => Assert.True(x.Length >= 32));
    }

    [Fact]
    public void Update_sets_password_only_when_missing()
    {
        _service.Create(User("ak-1", "john"), BaseUrl);
        var user = _store.Users.Single();
        _store.Passwords.Remove(user.Id);

        _service.Replace(user.Id.ToString(), User("ak-1", "john"), BaseUrl);
        Assert.True(_store.HasPassword(user.Id));

        _store.Passwords[user.Id] = "set-by-operator";
        _service.Replace(user.Id.ToString(), User("ak-1", "john"), BaseUrl);
        Assert.Equal("set-by-operator", _store.Passwords[user.Id]);
    }

    [Fact]
    public void Random_password_can_be_turned_off()
    {
        var service = new ScimUserService(_store, new ScimSettings { SetRandomPassword = false });
        service.Create(User("ak-1", "john"), BaseUrl);

        Assert.Empty(_store.Passwords);
    }

    [Fact]
    public void Replace_renames_and_keeps_collision_suffix()
    {
        _store.AddUnmanaged("john");
        _service.Create(User("ak-1", "john"), BaseUrl);
        var user = _store.Users.Single(x => x.ExternalId == "ak-1");
        Assert.Equal("john_2", user.Username);

        // Same name again: keep the suffixed name, no update.
        _service.Replace(user.Id.ToString(), User("ak-1", "john"), BaseUrl);
        Assert.Equal("john_2", user.Username);
        Assert.Equal(0, _store.UpdateCalls);

        _service.Replace(user.Id.ToString(), User("ak-1", "Johnny B"), BaseUrl);
        Assert.Equal("Johnny_B", user.Username);
    }

    [Fact]
    public void Replace_unknown_or_unmanaged_user_returns_404()
    {
        var clubUser = _store.AddUnmanaged("someone");
        Assert.Equal(404, _service.Replace(clubUser.Id.ToString(), User("ak-1", "x"), BaseUrl).Status);
        Assert.Equal(404, _service.Replace("999", User("ak-1", "x"), BaseUrl).Status);
    }

    [Fact]
    public void Delete_disables_instead_of_deleting()
    {
        _service.Create(User("ak-1", "john"), BaseUrl);
        var id = _store.Users.Single().Id.ToString();

        Assert.Equal(204, _service.Delete(id).Status);
        Assert.True(_store.Users.Single().IsDisabled);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public void Delete_mode_delete_soft_deletes_user()
    {
        var service = new ScimUserService(_store, new ScimSettings { DeleteMode = DeleteMode.Delete });
        service.Create(User("ak-1", "john"), BaseUrl);
        var id = _store.Users.Single().Id;

        Assert.Equal(204, service.Delete(id.ToString()).Status);
        Assert.Contains(id, _store.Deleted);
        Assert.False(_store.Users.Single().IsDisabled);
        Assert.Equal(404, service.Get(id.ToString(), BaseUrl).Status);
    }

    [Fact]
    public void Create_restores_deleted_user_with_same_external_id()
    {
        var service = new ScimUserService(_store, new ScimSettings { DeleteMode = DeleteMode.Delete });
        service.Create(User("ak-1", "john"), BaseUrl);
        var id = _store.Users.Single().Id;
        service.Delete(id.ToString());

        var response = service.Create(User("ak-1", "john"), BaseUrl);

        Assert.Equal(201, response.Status);
        var user = Assert.Single(_store.Users);
        Assert.Equal(id, user.Id);
        Assert.Equal("john", user.Username);
        Assert.Empty(_store.Deleted);
    }

    [Fact]
    public void Attributes_only_allowed_names_are_written_and_missing_ones_removed()
    {
        var body = User("ak-1", "john");
        body[ScimConstants.GizmoExtensionSchema] = new JsonObject
        {
            ["attributes"] = new JsonObject { ["Level"] = "5", ["Secret"] = "x" },
        };
        _service.Create(body, BaseUrl);
        var user = _store.Users.Single();
        Assert.Equal("5", user.Attributes["Level"]);
        Assert.False(user.Attributes.ContainsKey("Secret"));

        var update = User("ak-1", "john");
        update[ScimConstants.GizmoExtensionSchema] = new JsonObject { ["attributes"] = new JsonObject() };
        _service.Replace(user.Id.ToString(), update, BaseUrl);
        Assert.False(user.Attributes.ContainsKey("Level"));
    }

    [Fact]
    public void Attributes_untouched_when_extension_not_sent()
    {
        var body = User("ak-1", "john");
        body[ScimConstants.GizmoExtensionSchema] =
            new JsonObject { ["attributes"] = new JsonObject { ["Level"] = "5" } };
        _service.Create(body, BaseUrl);
        var user = _store.Users.Single();

        _service.Replace(user.Id.ToString(), User("ak-1", "john"), BaseUrl);

        Assert.Equal("5", user.Attributes["Level"]);
    }

    [Fact]
    public void Filter_by_username_finds_normalized_name()
    {
        _service.Create(User("ak-1", "John Doe"), BaseUrl);

        var response = _service.List("userName eq \"John Doe\"", null, null, BaseUrl);

        Assert.Equal(1, response.Body!["totalResults"]!.GetValue<int>());
    }

    [Fact]
    public void List_returns_only_managed_users()
    {
        _store.AddUnmanaged("club");
        _service.Create(User("ak-1", "john"), BaseUrl);

        var response = _service.List(null, null, null, BaseUrl);

        Assert.Equal(1, response.Body!["totalResults"]!.GetValue<int>());
    }

    [Fact]
    public void Patch_active_false_disables_user()
    {
        _service.Create(User("ak-1", "john"), BaseUrl);
        var id = _store.Users.Single().Id.ToString();
        var patch = JsonNode.Parse("""{"Operations":[{"op":"replace","path":"active","value":false}]}""")!.AsObject();

        var response = _service.Patch(id, patch, BaseUrl);

        Assert.Equal(200, response.Status);
        Assert.True(_store.Users.Single().IsDisabled);
    }

    [Fact]
    public void Long_fields_are_clipped_to_gizmo_columns()
    {
        _service.Create(User("ak-1", "john", given: new string('a', 100), phone: "+7 (999) 123-45-67 ext 1234"),
            BaseUrl);
        var user = _store.Users.Single();
        Assert.Equal(FieldLimits.FirstName, user.FirstName!.Length);
        Assert.Equal(FieldLimits.MobilePhone, user.MobilePhone!.Length);
    }

    private static JsonObject User(string externalId, string userName, string? email = null, bool active = true,
        string? given = null, string? phone = null)
    {
        var json = new JsonObject
        {
            ["schemas"] = new JsonArray(ScimConstants.UserSchema),
            ["externalId"] = externalId,
            ["userName"] = userName,
            ["active"] = active,
            ["name"] = new JsonObject { ["givenName"] = given ?? "John", ["familyName"] = "Doe" },
        };

        if (email is not null)
        {
            json["emails"] = new JsonArray(new JsonObject { ["value"] = email, ["primary"] = true });
        }

        if (phone is not null)
        {
            json["phoneNumbers"] = new JsonArray(new JsonObject { ["value"] = phone, ["type"] = "mobile" });
        }

        return json;
    }
}
