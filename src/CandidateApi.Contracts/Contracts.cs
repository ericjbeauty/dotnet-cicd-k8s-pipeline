namespace CandidateApi.Contracts;

public sealed record DependencyStatus(string Name, string Type, bool Healthy);

public sealed record ReadinessReport(
    string Status,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<DependencyStatus> Dependencies);

public sealed record WorkItemResponse(
    int Id,
    string Title,
    string Team,
    string Priority,
    string Status);

public sealed record ServiceMetadataResponse(
    string Service,
    string Environment,
    string Region,
    string Version,
    DateTimeOffset TimeUtc);
