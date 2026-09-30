using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers.Memory;

public interface IVoiceManager
{
    Task<string> ListVoicesAsync(CancellationToken cancellationToken);

    Task<string> GetVoiceAsync(string? voice, string? audience, string? chat, CancellationToken cancellationToken);

    Task<string> SetVoiceAsync(string name, string? description, string? guide, string? rulesJson, bool? isDefault, CancellationToken cancellationToken);

    Task<string> SetVariantAsync(string voice, string category, string? guide, string? rulesJson, IReadOnlyList<string>? examples, CancellationToken cancellationToken);

    Task<string> ImportVoiceAsync(
        string name, string markdown, IReadOnlyDictionary<string, string>? sections, string? description, bool isDefault, CancellationToken cancellationToken);

    Task<string> ExportVoiceAsync(string name, CancellationToken cancellationToken);

    Task<string> DeleteVoiceAsync(string name, string? category, IUserConfirmation? confirmation, CancellationToken cancellationToken);

    Task<string> CheckVoiceAsync(string draft, string? chat, string? audience, string? voice, CancellationToken cancellationToken);

    Task<string> LearnVoiceAsync(string voice, string category, IReadOnlyList<string> sourceChats, int messagesPerChat, CancellationToken cancellationToken);
}
