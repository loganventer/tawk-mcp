using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients;

/// <summary>Asks the user a yes or no question through MCP elicitation, so the model cannot answer it.</summary>
public sealed class ElicitationConfirmation(Func<ElicitRequestParams, CancellationToken, ValueTask<ElicitResult>>? elicit) : IUserConfirmation
{
    public const string FieldName = "confirm";

    public static ElicitationConfirmation For(McpServer? server) =>
        new(server?.ClientCapabilities?.Elicitation is null ? null : server.ElicitAsync);

    public async Task<ConfirmationAnswer> AskAsync(string summary, CancellationToken cancellationToken)
    {
        if (elicit is null)
        {
            return ConfirmationAnswer.NotSupported;
        }

        var request = new ElicitRequestParams
        {
            Message = "tawk asks you to confirm: " + summary
                + "\n\nIf you say yes, tawk will show its own warning so you can confirm once more.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    [FieldName] = new ElicitRequestParams.BooleanSchema
                    {
                        Title = "Go ahead",
                        Description = "Tick to go ahead. Leave it clear to cancel.",
                        Default = false,
                    },
                },
                Required = [FieldName],
            },
        };

        ElicitResult result;
        try
        {
            result = await elicit(request, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return ConfirmationAnswer.NotSupported;
        }
        catch (McpException)
        {
            return ConfirmationAnswer.Cancelled;
        }

        return result.Action switch
        {
            "accept" when result.Content is { } content
                && content.TryGetValue(FieldName, out var value)
                && value.ValueKind == JsonValueKind.True => ConfirmationAnswer.Accepted,
            "cancel" => ConfirmationAnswer.Cancelled,
            _ => ConfirmationAnswer.Declined,
        };
    }
}
