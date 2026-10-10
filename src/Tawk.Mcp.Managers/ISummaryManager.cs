namespace Tawk.Mcp.Managers;

/// <summary>Hands the TL;DR of a long message to tawk, which keeps it and shows it in place of the text.</summary>
public interface ISummaryManager
{
    /// <summary>Keeps <paramref name="text"/> as the summary of the message, and says so as text for an agent.</summary>
    Task<string> KeepAsync(string messageId, string text, string? model, CancellationToken cancellationToken);
}
