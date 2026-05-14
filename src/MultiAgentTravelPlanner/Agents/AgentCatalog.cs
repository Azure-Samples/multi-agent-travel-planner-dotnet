using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MultiAgentTravelPlanner.Tools;
using OpenAI.Chat;

namespace MultiAgentTravelPlanner.Agents;

/// <summary>
/// Builds the coordinator and the specialized agents. Each AIAgent is wrapped
/// with <c>UseOpenTelemetry</c> so its runs emit the OpenTelemetry GenAI
/// semantic-convention spans (<c>gen_ai.agent.name</c>, <c>gen_ai.agent.id</c>,
/// <c>gen_ai.usage.*</c>) that the App Service Agents tab and the
/// Application Insights Agents (preview) view group on.
/// </summary>
public sealed class AgentCatalog
{
    public const string TelemetrySourceName = "MultiAgentTravelPlanner.Agents";

    public required AIAgent Coordinator { get; init; }
    public required IReadOnlyList<AIAgent> Specialists { get; init; }

    public static AgentCatalog Build(AzureOpenAIClient client, string deployment, TravelTools tools)
    {
        ChatClient chat = client.GetChatClient(deployment);

        AIAgent weather = WithTelemetry(Agent(chat,
            id: "WeatherAdvisor",
            name: "WeatherAdvisor",
            description: "Provides weather forecasts, packing recommendations, and activity suggestions based on destination weather conditions.",
            instructions: "You are a concise weather and packing advisor. Use the weather tool to look up the forecast before answering. Recommend appropriate clothing and one or two activities suited to the conditions.",
            tools: [AIFunctionFactory.Create(tools.GetWeatherForecast)]));

        AIAgent currency = WithTelemetry(Agent(chat,
            id: "CurrencyConverter",
            name: "CurrencyConverter",
            description: "Handles currency conversion, exchange rate information, and budget allocation across different currencies for travelers.",
            instructions: "You are a currency conversion specialist. Use the exchange-rate tool and present results in a short summary table.",
            tools: [AIFunctionFactory.Create(tools.GetExchangeRate)]));

        AIAgent budget = WithTelemetry(Agent(chat,
            id: "BudgetOptimizer",
            name: "BudgetOptimizer",
            description: "Optimizes travel budget allocation across accommodation, food, activities, and transport with cost-saving strategies.",
            instructions: "You optimize a travel budget across lodging, food, activities, and transport. Use the daily-budget tool to ground your suggestions and propose at most three cost-saving tips.",
            tools: [AIFunctionFactory.Create(tools.GetDailyBudget)]));

        AIAgent local = WithTelemetry(Agent(chat,
            id: "LocalKnowledge",
            name: "LocalKnowledge",
            description: "Provides destination-specific knowledge including cultural insights, safety tips, local transportation, and authentic experiences.",
            instructions: "You are a local expert and cultural guide. Use the cultural-tip tool when relevant. Keep responses under 120 words.",
            tools: [AIFunctionFactory.Create(tools.GetCulturalTip)]));

        AIAgent itinerary = WithTelemetry(Agent(chat,
            id: "ItineraryPlanner",
            name: "ItineraryPlanner",
            description: "Creates detailed day-by-day travel itineraries with timing, venues, meal recommendations, and weather considerations.",
            instructions: "You build a clear day-by-day itinerary. Be specific about venues and timing. Keep it to 3–5 days unless the user asks for more."));

        var specialists = new[] { weather, currency, budget, local, itinerary };

        // The coordinator delegates by exposing each specialist as a callable tool.
        AIAgent coordinator = WithTelemetry(Agent(chat,
            id: "Coordinator",
            name: "Coordinator",
            description: "Coordinates the multi-agent travel planning workflow and aggregates results from specialized agents into a complete travel plan.",
            instructions:
                "You are the travel planning coordinator. Decompose the user's request and call the appropriate specialist agents " +
                "(WeatherAdvisor, CurrencyConverter, BudgetOptimizer, LocalKnowledge, ItineraryPlanner) in parallel where possible. " +
                "Aggregate their answers into a single, well-structured travel plan. Always cite which specialist contributed which section.",
            tools: specialists.Select(s => (AITool)s.AsAIFunction()).ToArray()));

        return new AgentCatalog { Coordinator = coordinator, Specialists = specialists };
    }

    // Stable agent identity so the Agents tab can aggregate calls across app
    // restarts and deployments. Without an explicit Id, every restart would
    // generate a new GUID for each agent and the tab would only show the
    // latest run's counts.
    private static AIAgent Agent(
        ChatClient chat,
        string id,
        string name,
        string description,
        string instructions,
        IList<AITool>? tools = null)
        => chat.AsAIAgent(new ChatClientAgentOptions
        {
            Id = id,
            Name = name,
            Description = description,
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools,
            },
        });

    private static AIAgent WithTelemetry(AIAgent agent) =>
        agent.AsBuilder()
            .UseOpenTelemetry(TelemetrySourceName, otel => otel.EnableSensitiveData = true)
            .Build();
}
