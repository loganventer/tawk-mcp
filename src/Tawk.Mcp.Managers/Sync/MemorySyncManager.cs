using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Engines.Sync;
using Tawk.Mcp.ResourceAccess.Memory;
using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Managers.Sync;

public sealed class MemorySyncManager(
    IMemoryRemote remote,
    IMemorySnapshotStore snapshots,
    IMemoryMerger merger,
    IMemoryDigest digest,
    ISyncStateStore state,
    ISyncLock syncLock,
    ISyncScratch scratch,
    IContactStore contacts,
    SyncOptions options,
    TimeProvider clock) : IMemorySyncManager
{
    private const int Attempts = 3;

    public async Task<SyncReport> SyncAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return new SyncReport(SyncOutcome.Disabled, "Memory sync is off: it needs a repository (TAWKMCP_SYNC_REPO) and a token (TAWKMCP_SYNC_TOKEN).");
        }

        using var held = syncLock.TryAcquire();
        if (held is null)
        {
            return new SyncReport(SyncOutcome.Busy, "Another tawk-mcp on this machine is syncing; this one skipped the cycle.");
        }

        try
        {
            return await CycleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is MemoryException or IOException or UnauthorizedAccessException)
        {
            return new SyncReport(SyncOutcome.Failed, "Memory sync failed: " + ex.Message);
        }
        finally
        {
            scratch.Clear();
        }
    }

    private async Task<SyncReport> CycleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();

        // Lapsed fields go first, so every machine compares the same rows.
        await contacts.PurgeExpiredAsync(now, cancellationToken).ConfigureAwait(false);
        var pulled = false;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var head = await remote.HeadAsync(cancellationToken).ConfigureAwait(false);
            var local = await snapshots.ReadAsync(cancellationToken).ConfigureAwait(false);
            var localDigest = digest.Compute(local);
            var last = state.Load();
            string? remoteDigest = null;
            if (head is not null && head == last.RemoteVersion && last.Digest is not null)
            {
                // The remote has not moved since this machine last matched it, so there is nothing to fetch.
                remoteDigest = last.Digest;
            }
            else if (head is not null)
            {
                var copy = scratch.NewFile();
                await remote.DownloadAsync(head, copy, cancellationToken).ConfigureAwait(false);
                var theirs = await snapshots.ReadFileAsync(copy, cancellationToken).ConfigureAwait(false);
                remoteDigest = digest.Compute(theirs);
                var plan = merger.Merge(local, theirs, now);
                if (!plan.IsEmpty)
                {
                    await snapshots.ApplyAsync(plan, cancellationToken).ConfigureAwait(false);
                    pulled = true;
                    localDigest = digest.Compute(await snapshots.ReadAsync(cancellationToken).ConfigureAwait(false));
                }
            }

            // Content is compared, not bytes: two machines holding the same memory never push at each other.
            if (localDigest == remoteDigest)
            {
                state.Save(new SyncState(head, localDigest));
                return pulled
                    ? new SyncReport(SyncOutcome.Pulled, "Merged the remote memory into this machine.")
                    : new SyncReport(SyncOutcome.UpToDate, "Memory is already in step with the remote.");
            }

            var upload = scratch.NewFile();
            await snapshots.BackupAsync(upload, cancellationToken).ConfigureAwait(false);
            var message = head is null ? "Add memory" : $"Sync memory (merged from {head[..Math.Min(7, head.Length)]})";
            var pushed = await remote.PushAsync(upload, head, message, cancellationToken).ConfigureAwait(false);
            if (pushed is not null)
            {
                state.Save(new SyncState(pushed, localDigest));
                return pulled
                    ? new SyncReport(SyncOutcome.PulledAndPushed, "Merged the remote memory and pushed this machine's changes.")
                    : new SyncReport(SyncOutcome.Pushed, "Pushed this machine's memory to the remote.");
            }
        }

        return new SyncReport(SyncOutcome.Failed, $"Memory sync failed: the remote changed {Attempts} times while this machine was pushing. The next cycle tries again.");
    }
}
