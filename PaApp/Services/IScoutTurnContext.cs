namespace PaApp.Services;

/// <summary>Per-request scope: current Mission Control session for tool side-effects.</summary>
public interface IScoutTurnContext
{
    Guid SessionId { get; set; }
}

public sealed class ScoutTurnContext : IScoutTurnContext
{
    public Guid SessionId { get; set; }
}
