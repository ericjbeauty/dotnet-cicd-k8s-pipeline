using CandidateApi.Configuration;
using CandidateApi.Contracts;
using CandidateApi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CandidateApiOptions>()
    .Bind(builder.Configuration.GetSection(CandidateApiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ReadinessEvaluator>();

var app = builder.Build();

app.MapGet("/", (IOptions<CandidateApiOptions> options, IWebHostEnvironment environment) =>
{
    var response = new ServiceMetadataResponse(
        options.Value.ServiceName,
        environment.EnvironmentName,
        options.Value.Region,
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
        DateTimeOffset.UtcNow);

    return Results.Ok(response);
});

app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }));

app.MapGet(
    "/health/ready",
    (IOptions<CandidateApiOptions> options, ReadinessEvaluator readinessEvaluator) =>
    {
        var report = readinessEvaluator.Evaluate(options.Value.Dependencies);
        return report.Status == "Healthy"
            ? Results.Ok(report)
            : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    });

app.MapGet("/api/work-items", (IOptions<CandidateApiOptions> options) =>
{
    var items = options.Value.WorkItems
        .Select(item => new WorkItemResponse(
            item.Id,
            item.Title,
            item.Team,
            item.Priority,
            item.Status))
        .ToArray();

    return Results.Ok(items);
});

app.Run();
