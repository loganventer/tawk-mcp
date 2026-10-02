namespace Tawk.Mcp.Core.Sync;

/// <summary>The record of a delete: the table the row was in, its key, and when it went, in Unix milliseconds.</summary>
public sealed record SyncTombstone(string Kind, string Key, long Deleted);
