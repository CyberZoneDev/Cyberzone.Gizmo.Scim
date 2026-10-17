using System.Text.Json;
using Cyberzone.Gizmo.Scim.Core.Sync;

namespace Cyberzone.Gizmo.Scim.V2.Infrastructure;

/// <summary>
/// Reads Cyberzone.Gizmo.Scim.V2.json from the folder of the plugin DLL and re-reads it when the file changes,
/// so the token can be rotated without restarting the Gizmo server.
/// </summary>
internal static class ModuleSettings
{
    private static readonly object Sync = new();
    private static ScimSettings? _cached;
    private static DateTime _cachedWriteTime;

    public static string FileName { get; } = typeof(ModuleSettings).Assembly.GetName().Name + ".json";

    public static string FilePath { get; } = Path.Combine(
        Path.GetDirectoryName(typeof(ModuleSettings).Assembly.Location) ?? AppContext.BaseDirectory,
        FileName);

    /// <summary>
    /// Current settings, or null when the settings file is missing or invalid.
    /// </summary>
    public static ScimSettings? Current
    {
        get
        {
            lock (Sync)
            {
                if (!File.Exists(FilePath))
                {
                    _cached = null;
                    return null;
                }

                var writeTime = File.GetLastWriteTimeUtc(FilePath);
                if (_cached is null || writeTime != _cachedWriteTime)
                {
                    _cached = JsonSerializer.Deserialize<ScimSettings>(
                        File.ReadAllText(FilePath),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        });
                    _cachedWriteTime = writeTime;
                }

                return _cached;
            }
        }
    }
}
