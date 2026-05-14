namespace MultiAgentTravelPlanner.Models;

public record ChatRequest(string Message, string? SessionId);

public record ChatResponse(string SessionId, string Reply);
