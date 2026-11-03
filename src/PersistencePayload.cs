using System.Text.Json;

namespace Based.Spires2DamageMeter;
public sealed class PersistencePayload
{
    public string Profile => Path == null ? throw new InvalidOperationException("Missing payload path") : Path.Replace('\\', '/')is string normalized && normalized.Contains('/') ? normalized[..normalized.LastIndexOf('/')] : ".";
    public string? Hash { get; internal set; }
    public string? Json { get; internal set; }
    public string? Path { get; internal set; }

    public void Capture(string path, string json) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("PersistencePayload.Capture", this, path, json);
    public static RunIdentity Identity(string profile, string json) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<RunIdentity>("PersistencePayload.Identity", profile, json);
}
