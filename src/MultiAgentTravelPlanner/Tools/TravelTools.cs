using System.ComponentModel;

namespace MultiAgentTravelPlanner.Tools;

/// <summary>
/// Mock travel-related tools used by the specialized agents. The values are
/// hard-coded so the sample runs without external dependencies. Each method
/// is decorated with <see cref="DescriptionAttribute"/> so Microsoft Agent
/// Framework can surface it as a function tool.
/// </summary>
public sealed class TravelTools
{
    [Description("Gets a short weather forecast for a destination on a given date.")]
    public string GetWeatherForecast(
        [Description("City and country, for example 'Lisbon, Portugal'.")] string destination,
        [Description("ISO-8601 date, for example '2026-07-12'.")] string date)
        => $"Forecast for {destination} on {date}: 24°C, partly cloudy, light breeze, 10% chance of rain.";

    [Description("Returns the current exchange rate between two ISO-4217 currency codes.")]
    public string GetExchangeRate(
        [Description("Source currency code, e.g. 'USD'.")] string from,
        [Description("Target currency code, e.g. 'EUR'.")] string to)
        => $"1 {from.ToUpperInvariant()} ≈ {(from.ToUpperInvariant() == to.ToUpperInvariant() ? 1.00 : 0.92):F4} {to.ToUpperInvariant()}";

    [Description("Suggests a daily budget range (in USD) for a destination by tier (budget|midrange|luxury).")]
    public string GetDailyBudget(
        [Description("City and country.")] string destination,
        [Description("'budget', 'midrange', or 'luxury'.")] string tier)
        => tier.ToLowerInvariant() switch
        {
            "budget"   => $"{destination}: about $60–$110/day for budget travelers.",
            "luxury"   => $"{destination}: $400+/day for luxury travelers.",
            _          => $"{destination}: about $150–$250/day for midrange travelers.",
        };

    [Description("Returns one or two cultural tips for the given destination.")]
    public string GetCulturalTip([Description("City and country.")] string destination)
        => $"Cultural tip for {destination}: greet shopkeepers when entering small stores, and learn 'hello' and 'thank you' in the local language.";
}
