namespace Tawk.Mcp.Core;

/// <summary>
/// How long a lookup of someone's online status waits for WhatsApp's answer: tawk asks WhatsApp on the
/// first call and usually knows a moment later, so the question is put again after each wait.
/// </summary>
public sealed record PresenceLookupOptions(int Retries = 3, TimeSpan Wait = default)
{
    public TimeSpan Wait { get; init; } = Wait == default ? TimeSpan.FromMilliseconds(700) : Wait;
}
