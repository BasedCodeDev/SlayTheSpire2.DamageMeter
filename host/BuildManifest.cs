using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Based.Spires2DamageMeter.Reload;

namespace Based.Spires2DamageMeter.Hosting;

public sealed record BuildManifest(string BuildId, string Version, string HostHash, string ContractsHash,
    int ApiVersion, int StateVersion, Dictionary<string, string> Files)
{
    public const string PayloadName = "Based.Spires2DamageMeter.Runtime.dll";
    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static string ContentId(IReadOnlyDictionary<string, string> files)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
            files.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value.ToUpperInvariant())))));

    public static BuildManifest Read(string path)
        => JsonSerializer.Deserialize<BuildManifest>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty build manifest");

    public string Validate(string installation)
    {
        if (ApiVersion != RuntimeContract.ApiVersion || StateVersion != RuntimeContract.StateVersion)
            throw new InvalidDataException("Incompatible runtime API/state version. Restart with a matching host.");
        if (string.IsNullOrWhiteSpace(BuildId) || BuildId.Length != 64 || BuildId.Any(c => !Uri.IsHexDigit(c)) || Files == null || Files.Count == 0 || !Files.ContainsKey(PayloadName) || string.IsNullOrWhiteSpace(Version))
            throw new InvalidDataException("Invalid runtime build identity");
        if (!StringComparer.OrdinalIgnoreCase.Equals(ContentId(Files), BuildId))
            throw new InvalidDataException("Runtime build identity does not match its contents");
        if (Version.Split('+')[0].Equals("0.3.0-preview.17", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("BLOCKED: withdrawn preview.17 runtime");
        if (!StringComparer.OrdinalIgnoreCase.Equals(HashFile(Path.Combine(installation, "Based.Spires2DamageMeter.dll")), HostHash) ||
            !StringComparer.OrdinalIgnoreCase.Equals(HashFile(Path.Combine(installation, "Based.Spires2DamageMeter.Contracts.dll")), ContractsHash))
            throw new InvalidDataException("Host or contracts changed. Close the game and reinstall the complete development build.");
        var directory = Path.Combine(installation, "runtime", "builds", BuildId);
        foreach (var file in Files)
        {
            if (Path.GetFileName(file.Key) != file.Key || file.Key.IndexOfAny(['/', '\\', ':']) >= 0 ||
                !(file.Key.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.Key.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) || file.Key.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Invalid runtime file name: " + file.Key);
            if (!StringComparer.OrdinalIgnoreCase.Equals(HashFile(Path.Combine(directory, file.Key)), file.Value))
                throw new InvalidDataException("Runtime file is incomplete or has changed: " + file.Key);
        }
        return Path.Combine(directory, PayloadName);
    }
}
