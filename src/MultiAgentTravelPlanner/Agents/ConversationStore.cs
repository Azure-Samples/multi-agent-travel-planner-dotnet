using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace MultiAgentTravelPlanner.Agents;

/// <summary>
/// In-memory store for per-session <see cref="AgentSession"/> objects. This is
/// intentionally simple — production apps should externalize state.
/// </summary>
public sealed class ConversationStore
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public async ValueTask<AgentSession> GetOrCreateAsync(
        string sessionId,
        AIAgent agent,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        var created = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        return _sessions.GetOrAdd(sessionId, created);
    }
}
