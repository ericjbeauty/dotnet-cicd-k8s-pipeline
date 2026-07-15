namespace CandidateApi.Configuration;

public sealed class CandidateApiOptions
{
    public const string SectionName = "CandidateApi";

    public string ServiceName { get; set; } = "candidate-api";

    public string Region { get; set; } = "local";

    public List<ConfiguredDependency> Dependencies { get; set; } = [];

    public List<ConfiguredWorkItem> WorkItems { get; set; } = [];
}

public sealed class ConfiguredDependency
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool Healthy { get; set; } = true;
}

public sealed class ConfiguredWorkItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Team { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
