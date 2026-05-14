using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Agents.AI;
using MultiAgentTravelPlanner.Agents;
using MultiAgentTravelPlanner.Models;
using MultiAgentTravelPlanner.Tools;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// OpenTelemetry + Azure Monitor distro
//
// The Azure Monitor distro auto-wires traces, logs, and metrics to App Insights
// when APPLICATIONINSIGHTS_CONNECTION_STRING is set. We extend it to listen to
// the Microsoft Agent Framework activity sources so per-agent spans/metrics
// flow with gen_ai.* attributes (the GenAI semantic conventions). The Agents
// (preview) view in App Insights groups and aggregates on those attributes.
// ---------------------------------------------------------------------------

// The Azure Monitor distro auto-reads APPLICATIONINSIGHTS_CONNECTION_STRING.
// UseAzureMonitor() is the entry point; subsequent .WithTracing/.WithMetrics
// calls on the same builder add the Microsoft Agent Framework activity
// sources and meters to the same pipeline.
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()
    .ConfigureResource(r => r
        .AddService(serviceName: "multi-agent-travel-planner"))
    .WithTracing(t => t
        // Source name used by Microsoft.Agents.AI's UseOpenTelemetry wrapper
        // in AgentCatalog. Must match the value passed to UseOpenTelemetry.
        .AddSource(MultiAgentTravelPlanner.Agents.AgentCatalog.TelemetrySourceName)
        // Microsoft.Extensions.AI emits OpenAI client + chat spans here
        .AddSource("Microsoft.Extensions.AI*")
        // OpenAI SDK client spans
        .AddSource("OpenAI*")
        .AddSource("Experimental.OpenAI*")
        .AddSource("Azure.AI.OpenAI*"))
    .WithMetrics(m => m
        .AddMeter(MultiAgentTravelPlanner.Agents.AgentCatalog.TelemetrySourceName)
        .AddMeter("Microsoft.Extensions.AI*")
        .AddMeter("OpenAI*"));

// ---------------------------------------------------------------------------
// Azure OpenAI client (managed identity, no keys)
// ---------------------------------------------------------------------------

var aoaiEndpoint = builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
var aoaiDeployment = builder.Configuration["AzureOpenAI:Deployment"]
    ?? throw new InvalidOperationException("AzureOpenAI:Deployment is required.");

builder.Services.AddSingleton(new AzureOpenAIClient(
    new Uri(aoaiEndpoint),
    new DefaultAzureCredential()));

// ---------------------------------------------------------------------------
// Agents
//
// Each AIAgent is a top-level entity that the Agents view groups telemetry by
// (via gen_ai.agent.name / gen_ai.agent.id). Names must match between the
// agent registration and the spans the framework emits.
// ---------------------------------------------------------------------------

builder.Services.AddSingleton<TravelTools>();
builder.Services.AddSingleton<AgentCatalog>(sp =>
{
    var client = sp.GetRequiredService<AzureOpenAIClient>();
    var tools = sp.GetRequiredService<TravelTools>();
    return AgentCatalog.Build(client, aoaiDeployment, tools);
});

// In-memory per-session conversation state. Keep it simple — this sample is
// about monitoring, not state management.
builder.Services.AddSingleton<ConversationStore>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.MapPost("/api/chat", async (
    ChatRequest req,
    AgentCatalog catalog,
    ConversationStore store,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Message))
    {
        return Results.BadRequest(new { error = "Message is required." });
    }

    var sessionId = string.IsNullOrWhiteSpace(req.SessionId)
        ? Guid.NewGuid().ToString("N")
        : req.SessionId!;
    var session = await store.GetOrCreateAsync(sessionId, catalog.Coordinator, ct);

    var response = await catalog.Coordinator.RunAsync(req.Message, session, cancellationToken: ct);

    return Results.Ok(new ChatResponse(sessionId, response.Text ?? string.Empty));
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();
