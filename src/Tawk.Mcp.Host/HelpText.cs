namespace Tawk.Mcp.Host;

public static class HelpText
{
    public const string Text = """
        tawk-mcp: an MCP server for tawk, the WhatsApp client for the terminal.

        Usage:
          tawk-mcp [--port N]           serve MCP over streamable HTTP at http://127.0.0.1:N/mcp (default 8765)
          tawk-mcp --stdio              serve MCP over stdio, for a client that starts tawk-mcp itself
          tawk-mcp print-token          print the bearer token for HTTP mode, creating it if needed
          tawk-mcp healthcheck          ask a running HTTP server for /healthz (exit 0 when it answers)
          tawk-mcp sync                 sync the memory database with its repository once, now
          tawk-mcp export-okf DIR       write knowledge as an Open Knowledge Format 0.2 bundle (--include-sensitive for all of it)
          tawk-mcp import-okf DIR       read an Open Knowledge Format bundle into memory
          tawk-mcp --version | --help

        Options (each also has an environment variable):
          --socket PATH                 tawk's control socket          TAWK_CONTROL_SOCKET
          --bind ADDRESS                HTTP bind address, 127.0.0.1   TAWKMCP_BIND
          --port N                      HTTP port, 8765                TAWKMCP_PORT
          --stdio | --http              transport, http by default     TAWKMCP_TRANSPORT
          --token-file PATH             bearer token file              TAWKMCP_TOKEN_FILE (or TAWKMCP_TOKEN)
          --channel auto|on|off         Claude Code channel events     TAWKMCP_CHANNEL
          --channel-own on|off          also the messages you send     TAWKMCP_CHANNEL_OWN (off)
          --memory write|read|off       voices, contacts, templates    TAWKMCP_MEMORY (write)
          --data-file PATH              memory database                TAWKMCP_DATA_FILE
          --sync-repo OWNER/NAME        private repository for memory  TAWKMCP_SYNC_REPO
          --sync-branch NAME            its branch, main               TAWKMCP_SYNC_BRANCH
          --sync-file PATH              the file in it, memory.db      TAWKMCP_SYNC_FILE
          --sync-api URL                GitHub API address             TAWKMCP_SYNC_API (https://api.github.com)
          --sync-interval-minutes N     minutes between syncs, 120     TAWKMCP_SYNC_INTERVAL_MINUTES
                                        sync runs only with a repository and a token in TAWKMCP_SYNC_TOKEN
          --workflow-every N            rounds between workflow checks TAWKMCP_WORKFLOW_EVERY (20, 0 = off)
          --instructions-file PATH      your standing instructions     TAWKMCP_INSTRUCTIONS_FILE
          --admin-token-file PATH       tawk's admin token file        TAWKMCP_ADMIN_TOKEN_FILE
                                        no default: only with it may this instance approve its own sends
          --schedule-jitter-s N         random +/- shift on schedules  TAWKMCP_SCHEDULE_JITTER_S (60, 0 = off)
          --backoff-initial-ms N        first retry wait, 500          TAWKMCP_BACKOFF_INITIAL_MS
          --backoff-max-ms N            longest retry wait, 30000      TAWKMCP_BACKOFF_MAX_MS
          --breaker-threshold N         failures before pausing, 5     TAWKMCP_BREAKER_THRESHOLD
          --breaker-cooldown-s N        pause length, 60               TAWKMCP_BREAKER_COOLDOWN_S
          --request-timeout-s N         read answer timeout, 10        TAWKMCP_REQUEST_TIMEOUT_S

        HTTP requests need "Authorization: Bearer <token>"; see tawk-mcp print-token.
        tawk must have Settings > Automation > Control socket turned on.
        """;
}
