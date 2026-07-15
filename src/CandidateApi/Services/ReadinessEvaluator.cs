using CandidateApi.Configuration;
using CandidateApi.Contracts;

namespace CandidateApi.Services;

public sealed class ReadinessEvaluator
{
    public ReadinessReport Evaluate(IEnumerable<ConfiguredDependency> dependencies)
    {
        var dependencyStatuses = dependencies
            .Select(dependency => new DependencyStatus(
                dependency.Name,
                dependency.Type,
                dependency.Healthy))
            .ToArray();

        var status = dependencyStatuses.All(dependency => dependency.Healthy)
            ? "Healthy"
            : "Degraded";

        return new ReadinessReport(status, DateTimeOffset.UtcNow, dependencyStatuses);
    }
}
