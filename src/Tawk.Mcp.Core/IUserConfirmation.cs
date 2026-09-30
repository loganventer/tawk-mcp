namespace Tawk.Mcp.Core;

/// <summary>Asks the user directly, outside the model, whether a destructive step may go ahead.</summary>
public interface IUserConfirmation
{
    Task<ConfirmationAnswer> AskAsync(string summary, CancellationToken cancellationToken);
}
