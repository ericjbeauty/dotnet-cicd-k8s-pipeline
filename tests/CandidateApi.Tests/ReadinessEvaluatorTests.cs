using CandidateApi.Configuration;
using CandidateApi.Services;

namespace CandidateApi.Tests;

public class ReadinessEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsHealthy_WhenAllDependenciesAreHealthy()
    {
        var evaluator = new ReadinessEvaluator();

        var report = evaluator.Evaluate(
        [
            new ConfiguredDependency { Name = "postgres", Type = "database", Healthy = true },
            new ConfiguredDependency { Name = "redis", Type = "cache", Healthy = true }
        ]);

        Assert.Equal("Healthy", report.Status);
        Assert.All(report.Dependencies, dependency => Assert.True(dependency.Healthy));
    }

    [Fact]
    public void Evaluate_ReturnsDegraded_WhenAnyDependencyIsUnhealthy()
    {
        var evaluator = new ReadinessEvaluator();

        var report = evaluator.Evaluate(
        [
            new ConfiguredDependency { Name = "postgres", Type = "database", Healthy = true },
            new ConfiguredDependency { Name = "billing-api", Type = "http", Healthy = false }
        ]);

        Assert.Equal("Degraded", report.Status);
        Assert.Contains(report.Dependencies, dependency => dependency.Name == "billing-api" && !dependency.Healthy);
    }
}
