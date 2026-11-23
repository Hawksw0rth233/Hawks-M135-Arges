using SPTarkov.Server.Core.Models.Spt.Mod;
namespace Hawks.Arges;
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "team.aminus.arges";
    public string Name { get; init; } = "Hawks's M135 Arges";
    public string Author { get; init; } = "Hawks";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.5");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.6");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/Hawksw0rth233/Hawks-M135-Arges";
    public string License { get; init; } = "MIT (mod code); third-party game assets excluded";
    public bool HasPrepatcher { get; init; } = false;
}


