using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ProfileManager(ITawkControl control, IConfirmationGate gate) : IProfileManager
{
    public async Task<string> GetProfileAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("get_profile", null, cancellationToken).ConfigureAwait(false));

    public async Task<string> SetProfileAsync(string? name, string? about, WriteContext context, CancellationToken cancellationToken)
    {
        var args = new JsonObject();
        if (name is not null)
        {
            args["name"] = name;
        }

        if (about is not null)
        {
            args["about"] = about;
        }

        if (args.Count == 0)
        {
            throw new TawkControlException(ControlErrorCode.BadRequest, "Give a name, an about text, or both.");
        }

        return WriteResults.Describe(
            await RunAsync("set_profile", args, context, cancellationToken).ConfigureAwait(false),
            _ => "tawk is updating your profile in the background.");
    }

    public async Task<string> SetProfilePhotoAsync(string file, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("set_profile_photo", new JsonObject { ["file"] = file }, context, cancellationToken).ConfigureAwait(false),
            _ => "Profile photo set.");

    public async Task<string> RemoveProfilePhotoAsync(WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("remove_profile_photo", null, context, cancellationToken).ConfigureAwait(false),
            _ => "Profile photo removed.");

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject? args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
