using System.Text.Json.Nodes;
using Cyberzone.Gizmo.Scim.Core.Scim;

namespace Cyberzone.Gizmo.Scim.Core.Tests;

public class ScimFilterAndPatchTests
{
    [Fact]
    public void Filter_parses_quoted_value()
    {
        var filter = ScimFilter.Parse("userName eq \"john \\\"x\\\" doe\"")!;
        Assert.True(filter.Is("username"));
        Assert.Equal("john \"x\" doe", filter.Value);
    }

    [Fact]
    public void Filter_rejects_complex_expressions()
    {
        var exception = Assert.Throws<ScimException>(() => ScimFilter.Parse("userName sw \"j\" and active eq true"));
        Assert.Equal("invalidFilter", exception.ScimType);
    }

    [Fact]
    public void Patch_replaces_simple_and_nested_paths()
    {
        var resource = JsonNode.Parse("""{"userName":"a","active":true,"name":{"givenName":"A"}}""")!.AsObject();
        var patch = JsonNode.Parse("""
                                   {"Operations":[
                                     {"op":"replace","path":"active","value":false},
                                     {"op":"Replace","path":"name.familyName","value":"B"},
                                     {"op":"replace","value":{"userName":"b"}}
                                   ]}
                                   """)!.AsObject();

        ScimPatch.Apply(resource, patch);

        var user = ScimUser.FromJson(resource);
        Assert.False(user.Active);
        Assert.Equal("b", user.UserName);
        Assert.Equal("A", user.GivenName);
        Assert.Equal("B", user.FamilyName);
    }

    [Fact]
    public void Patch_handles_value_filter_paths()
    {
        var resource = JsonNode.Parse("""{"emails":[{"value":"old@x","type":"work","primary":true}]}""")!.AsObject();
        var patch = JsonNode.Parse("""
                                   {"Operations":[{"op":"replace","path":"emails[type eq \"work\"].value","value":"new@x"}]}
                                   """)!.AsObject();

        ScimPatch.Apply(resource, patch);

        Assert.Equal("new@x", ScimUser.FromJson(resource).Email);
    }

    [Fact]
    public void Patch_sets_and_removes_extension_attributes()
    {
        var resource = new JsonObject
        {
            [ScimConstants.GizmoExtensionSchema] =
                new JsonObject { ["attributes"] = new JsonObject { ["Card"] = "1" } },
        };
        var patch = JsonNode.Parse($$"""
                                     {"Operations":[
                                       {"op":"add","path":"{{ScimConstants.GizmoExtensionSchema}}:attributes.Level","value":"5"},
                                       {"op":"remove","path":"{{ScimConstants.GizmoExtensionSchema}}:attributes.Card"}
                                     ]}
                                     """)!.AsObject();

        ScimPatch.Apply(resource, patch);

        var attributes = ScimUser.FromJson(resource).Attributes!;
        Assert.Equal("5", attributes["Level"]);
        Assert.False(attributes.ContainsKey("Card"));
    }
}
