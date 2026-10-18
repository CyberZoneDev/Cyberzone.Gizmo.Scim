# Cyberzone.Gizmo.Scim

SCIM 2.0 endpoint inside the Gizmo server. Authentik (SCIM provider) pushes club customers into Gizmo;
Gizmo never calls Authentik.

## Layout

| Path                                    | Target  | What                                                                                                |
|-----------------------------------------|---------|-----------------------------------------------------------------------------------------------------|
| `src/Cyberzone.Gizmo.Scim.Core`         | net6.0  | Version-independent SCIM logic: parsing, filter, PATCH, username normalization, sync rules          |
| `src/Cyberzone.Gizmo.Scim.V2`           | net6.0  | Gizmo v2 plugin: `ScimModulePlugin`, controllers, `GizmoV2UserStore` (Core sources are compiled in) |
| `tests/Cyberzone.Gizmo.Scim.Core.Tests` | net8.0  | xUnit tests for Core                                                                                |
| `src/Cyberzone.Gizmo.Scim.V3`           | net10.0 | Planned                                                                                             |

Gizmo assemblies are not committed. The V2 project references them from the local server folder.

## Build

1. Copy `Directory.Build.props.user.example` to `Directory.Build.props.user` and set `GizmoV2Dir`
   to the folder that contains `GizmoService.dll` of the v2 server.
2. `dotnet build -c Release` (the solution is `Cyberzone.Gizmo.Scim.slnx`, needs .NET SDK 9.0.200+ or Visual Studio
   17.13+)
3. `dotnet test`

## Deploy (Gizmo v2)

The module is a regular Gizmo service plugin (`IGizmoServiceModulePlugin`). On start it adds its
controllers to the server's own MVC, so SCIM is served on the web portal port and certificate.

1. Copy `Cyberzone.Gizmo.Scim.V2.dll` into `<Gizmo Server>\plugins\`.
2. Next to it, copy `Cyberzone.Gizmo.Scim.V2.example.json` to `Cyberzone.Gizmo.Scim.V2.json`
   and set a long random `BearerToken`.
3. Restart the Gizmo service. The plugin appears in Manager (enabled, scope Global by default).
4. In Manager set the plugin scope to **Server only** (otherwise clients and Manager download it too).
   The server keeps it loaded, no extra restart is needed.
5. Check `GET https://<gizmo>:<port>/scim/v2/ServiceProviderConfig` with `Authorization: Bearer <token>`.
   The server log has `[SCIM] Endpoint registered at /scim/v2.` on start.

Disabling the plugin in Manager removes the endpoint after the next service restart.
The settings file is re-read when it changes, no restart needed.

| Key                    | Default       | Meaning                                                                                              |
|------------------------|---------------|------------------------------------------------------------------------------------------------------|
| `BearerToken`          |               | Token Authentik sends. Empty token = endpoint returns 503                                            |
| `LinkAttributeName`    | `AuthentikId` | Gizmo user attribute that stores SCIM `externalId` (Authentik user uid). Created if missing          |
| `AllowedAttributes`    | `[]`          | Gizmo attribute names Authentik may write via the extension schema. Others are ignored               |
| `PublicBaseUrl`        | `""`          | External URL of `/scim/v2` behind the proxy, used in `meta.location`. Empty = taken from the request |
| `RejectDuplicateEmail` | `false`       | `true`: 409 when the email belongs to another user. `false`: user is saved without email             |
| `DeleteMode`           | `Disable`     | What `DELETE` does: `Disable`, or `Delete` (Gizmo soft delete, also removes the profile folder)      |
| `SetRandomPassword`    | `true`        | Give synced users without a password a random one. `false`: they can log in with any password        |

## Authentik

1. Create a SCIM provider: URL `https://<public host>/scim/v2`, token = `BearerToken`.
2. Bind it to an application and restrict users (e.g. a group of club customers). Group sync can stay on:
   groups are accepted and ignored.
3. Reverse proxy: forward only `/scim/v2/*` to the Gizmo web portal port and pass the `Authorization` header through.

Extra attributes go through a custom SCIM property mapping added to the provider, for example:

```python
return {
    "urn:cyberzone:params:scim:schemas:extension:gizmo:2.0:User": {
        "attributes": {
            "JuliaId": request.user.attributes.get("julia_id", ""),
        }
    }
}
```

Each name must be listed in `AllowedAttributes`. An allowed attribute missing from a sent extension is removed in Gizmo.

## Sync rules

- SCIM `id` = Gizmo `UserId`; link attribute = `externalId`. Only linked users are visible through SCIM,
  so existing club accounts are never matched by name or email.
- `POST /Users` with an already linked `externalId` updates that user instead of creating a duplicate.
  A deleted user with that `externalId` is restored (undeleted) first.
- New users get the default user group (`IsDefault`). Groups are never changed afterwards.
- `userName` is corrected to Gizmo rules: max 30 chars, no spaces or `\ / : * ? " < > |`, no trailing dot,
  no reserved Windows names. Taken names get `_2`, `_3`, ...
- Gizmo lets a customer without a password log in with any password. With `SetRandomPassword` the plugin sets
  a random password on create and on the next update of a user that has none; a password set in Gizmo is kept.
  Passwords are never synced from Authentik.
- Synced fields: userName, name.givenName, name.familyName, primary email, mobile phone, active, allowed attributes.
  Everything else (balance, group, notes, address, ...) is left as is.
- `DELETE /Users/{id}` disables the user (`DeleteMode: Disable`) or soft-deletes it (`DeleteMode: Delete`).
  Gizmo removes the profile folder of a deleted user from disk, and undelete does not bring it back.
  Authentik also sends `DELETE` when a user just leaves the provider's scope, so a scope mistake deletes many users.
- Writes go through the same `Service` methods as the operator console (`UserCreate`, `UserUpdate`, `UserDisable`, ...),
  so server events and profile folder moves happen as usual.
