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
          tawk-mcp fetch-model NAME     download a Whisper model ahead of time: tiny, base, small,
                                        medium, large-v3-turbo or large-v3 (otherwise the first
                                        transcription that needs a model downloads it)
          tawk-mcp export-okf DIR       write knowledge as an Open Knowledge Format 0.2 bundle (--include-sensitive for all of it)
          tawk-mcp import-okf DIR       read an Open Knowledge Format bundle into memory
          tawk-mcp --version | --help

        Options (each also has an environment variable):
          --socket PATH                 tawk's control socket          TAWK_CONTROL_SOCKET
          --bind ADDRESS                HTTP bind address, 127.0.0.1   TAWKMCP_BIND
          every option and command below may be written with two dashes, one or none:
          --port, -port and port are the same
          --port N                      HTTP port, 8765                TAWKMCP_PORT
          --stdio | --http              transport, http by default     TAWKMCP_TRANSPORT
          --token-file PATH             bearer token file              TAWKMCP_TOKEN_FILE (or TAWKMCP_TOKEN)
          --channel auto|on|off         Claude Code channel events     TAWKMCP_CHANNEL
          --channel-own on|off          also the messages you send     TAWKMCP_CHANNEL_OWN (off)
          --channel-read on|off         also read receipts for them    TAWKMCP_CHANNEL_READ (off)
          --channel-reactions on|off    also reactions to them         TAWKMCP_CHANNEL_REACTIONS (off)
          --channel-edits on|off        also others' edits and deletes TAWKMCP_CHANNEL_EDITS (off)
          --channel-scheduled on|off    also scheduled messages going  TAWKMCP_CHANNEL_SCHEDULED (off)
          --memory write|read|off       voices, contacts, templates    TAWKMCP_MEMORY (write)
          --data-file PATH              memory database                TAWKMCP_DATA_FILE
          --sync-repo ADDRESS           private repository for memory  TAWKMCP_SYNC_REPO
                                        owner/name on GitHub, or an SSH address (git@host:owner/name.git)
          --sync-key PATH               SSH private key to use         TAWKMCP_SYNC_KEY (SSH chooses by default)
          --sync-branch NAME            its branch, main               TAWKMCP_SYNC_BRANCH
          --sync-file PATH              the file in it, memory.db      TAWKMCP_SYNC_FILE
          --sync-interval-minutes N     minutes between syncs, 120     TAWKMCP_SYNC_INTERVAL_MINUTES
                                        sync runs only with a repository set; it uses git over SSH, no tokens
          --workflow-every N            rounds between workflow checks TAWKMCP_WORKFLOW_EVERY (20, 0 = off)
          --instructions-file PATH      your standing instructions     TAWKMCP_INSTRUCTIONS_FILE
          --admin-token-file PATH       tawk's admin token file        TAWKMCP_ADMIN_TOKEN_FILE
                                        no default: only with it may this instance approve its own sends
          --transcribe MODE             voice notes to text            TAWKMCP_TRANSCRIBE (auto)
                                        auto: your transcriber at --transcribe-url while it answers,
                                        and otherwise one model loaded inside tawk-mcp
                                        embedded: always the model inside tawk-mcp
                                        http, command: only your own transcriber    off: none
          --transcribe-model-dir DIR    models for the one inside      TAWKMCP_TRANSCRIBE_MODEL_DIR
                                        ~/.local/share/tawk-mcp/models; a missing model is downloaded
          --transcribe-idle-unload-m N  let that model go when idle    TAWKMCP_TRANSCRIBE_IDLE_UNLOAD_M (15, 0 = never)
          --transcribe-url ADDRESS      the http transcriber's address TAWKMCP_TRANSCRIBE_URL
                                        speaks the OpenAI audio API; on this machine unless remote is on
          --transcribe-remote on|off    allow one on another machine   TAWKMCP_TRANSCRIBE_REMOTE (off)
          --transcribe-command TEXT     program for the command engine TAWKMCP_TRANSCRIBE_COMMAND
                                        {file} {language} {model} {task} {prompt} are filled in; no shell
                                        tawk's own Settings, Automation, Voice note transcription
                                        override auto, model and language; these are for an older tawk
          --transcribe-auto on|off      transcribe every voice note    TAWKMCP_TRANSCRIBE_AUTO
                                        that others send, unasked, in the default languages
          --transcribe-model NAME       default model                  TAWKMCP_TRANSCRIBE_MODEL
                                        large-v3-turbo unless chosen in tawk or here; tiny is the lightest
          --transcribe-models A,B       models an agent may ask for    TAWKMCP_TRANSCRIBE_MODELS
          --transcribe-language A,B     default languages, or auto     TAWKMCP_TRANSCRIBE_LANGUAGE
          --transcribe-max-languages N  most languages in one call, 3  TAWKMCP_TRANSCRIBE_MAX_LANGUAGES
          --transcribe-max-seconds N    longest recording, 3600        TAWKMCP_TRANSCRIBE_MAX_SECONDS
          --transcribe-timeout-s N      longest pass, 300              TAWKMCP_TRANSCRIBE_TIMEOUT_S
          --transcribe-concurrency N    jobs at once, 1                TAWKMCP_TRANSCRIBE_CONCURRENCY
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
