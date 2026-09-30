# Configuration

## Table of Contents

- [Where things live](#where-things-live)
- [Settings in tawk](#settings-in-tawk)
- [Arguments and environment variables](#arguments-and-environment-variables)
- [Commands](#commands)
- [The control socket](#the-control-socket)
- [The bearer token](#the-bearer-token)
- [Resilience](#resilience)
- [Docker](#docker)
- [MCP protocol revision](#mcp-protocol-revision)

## Where things live

| Path | Contents |
| --- | --- |
| `~/.local/bin/tawk-mcp` | The program, as `install.sh` puts it (`--prefix` changes the folder) |
| `~/.config/systemd/user/tawk-mcp.service` | The user service the installer registers on Linux with systemd |
| `~/Library/LaunchAgents/com.loganventer.tawk-mcp.plist` | The LaunchAgent the installer registers on macOS; its log is `~/Library/Logs/tawk-mcp.log` |
| `~/.config/tawk-mcp/token` | The bearer token for HTTP mode (file 0600, folder 0700). `$XDG_CONFIG_HOME` is used instead of `~/.config` when set |
| `$XDG_RUNTIME_DIR/tawk/control.sock` | tawk's control socket, owned by tawk. `~/.local/state/tawk/control.sock` when `XDG_RUNTIME_DIR` is not set |

tawk-mcp keeps nothing else on disk: no cache, no log file, no copy of your messages. Logs go to stderr.

## Settings in tawk

What a client may see and do is decided in tawk, under `[automation]` in `~/.config/tawk/config.ini` or **Settings > Automation**:

| Key | Values | Meaning |
| --- | --- | --- |
| `control_socket` | `on`, `off` | The socket itself (Settings > Automation > Control socket). Off by default |
| `access` | `read`, `send`, `manage` | What clients may do (Settings > Automation > Agent access). `read` by default |
| `chats` | a comma separated list | When set, only these chats are visible to clients |
| `confirm_cli` | `on`, `off` | Also ask before writes from your own tawk commands. Writes from tawk-mcp always ask |
| `writes_per_minute` | 1 to 60 | How many writes clients may make per minute |

tawk-mcp cannot change any of these. What agents ask for and do is listed in tawk's Agentic tab (**🤖 Agentic** in the header, or F3).

## Arguments and environment variables

Flags override environment variables, which override the defaults.

| Flag | Environment variable | Default | Meaning |
| --- | --- | --- | --- |
| `--http`, `--stdio` | `TAWKMCP_TRANSPORT` (`http`, `stdio`) | `http` | How MCP clients connect: streamable HTTP, or stdio for a client that starts tawk-mcp itself |
| `--port N` | `TAWKMCP_PORT` | `8765` | HTTP port |
| `--bind ADDRESS` | `TAWKMCP_BIND` | `127.0.0.1` | HTTP bind address. The token is required whatever the address |
| `--token-file PATH` | `TAWKMCP_TOKEN_FILE` | `~/.config/tawk-mcp/token` | Where the bearer token is kept |
| | `TAWKMCP_TOKEN` | | Use this token and keep no file, for container secrets |
| `--socket PATH` | `TAWK_CONTROL_SOCKET` | see [The control socket](#the-control-socket) | tawk's control socket |
| `--channel auto\|on\|off` | `TAWKMCP_CHANNEL` | `auto` | Claude Code channel events: `auto` for clients that identify as Claude Code, `on` for every client, `off` for none |
| `--backoff-initial-ms N` | `TAWKMCP_BACKOFF_INITIAL_MS` | `500` | First wait between connection attempts (1 to 600000) |
| `--backoff-max-ms N` | `TAWKMCP_BACKOFF_MAX_MS` | `30000` | Longest wait (1 to 3600000, not below the first) |
| `--breaker-threshold N` | `TAWKMCP_BREAKER_THRESHOLD` | `5` | Failures in a row before retries pause (1 to 1000) |
| `--breaker-cooldown-s N` | `TAWKMCP_BREAKER_COOLDOWN_S` | `60` | Length of the pause in seconds (0 to 86400) |
| `--request-timeout-s N` | `TAWKMCP_REQUEST_TIMEOUT_S` | `10` | How long a read waits for tawk before tawk counts as hung (1 to 600) |

A value out of range or a flag tawk-mcp does not know stops it at start with a message on stderr and exit code 2.

`ASPNETCORE_URLS` has no effect: HTTP mode listens only where `TAWKMCP_BIND` and `TAWKMCP_PORT` say.

## Commands

| Command | Does |
| --- | --- |
| `tawk-mcp` | Serves MCP over streamable HTTP at `http://127.0.0.1:8765/mcp`, with `/events` and `/healthz` |
| `tawk-mcp --stdio` | Serves MCP over stdio, for a client that starts tawk-mcp itself (and for Claude Code channels) |
| `tawk-mcp print-token` | Prints the bearer token, creating it if needed |
| `tawk-mcp healthcheck` | Asks a running HTTP tawk-mcp for `/healthz` on 127.0.0.1 and the configured port. Exit code 0 when it answers |
| `tawk-mcp --version` | Prints the version |
| `tawk-mcp --help` | Prints a summary of the flags |

## The control socket

tawk-mcp looks for tawk's socket in this order:

1. `--socket PATH`
2. `TAWK_CONTROL_SOCKET`
3. `$XDG_RUNTIME_DIR/tawk/control.sock`
4. `~/.local/state/tawk/control.sock`

tawk only accepts connections from a process running as the same user, so tawk-mcp must run as you, in Docker too.

## The bearer token

HTTP mode requires `Authorization: Bearer <token>` on every request except `GET /healthz`. The token is compared in constant time.

On first use tawk-mcp creates 32 random bytes, writes them base64url encoded (43 characters) to the token file with mode 0600, creating its folder with mode 0700, and keeps using that token. `tawk-mcp print-token` prints it. To change it, delete the file and restart tawk-mcp. `TAWKMCP_TOKEN` replaces the file entirely.

Requests with an `Origin` header other than `http://localhost`, `https://localhost`, `http://127.0.0.1` or `https://127.0.0.1` (any port) are refused with 403.

## Resilience

Connection attempts back off exponentially from `backoff-initial-ms`, doubling up to `backoff-max-ms`, each wait drawn at random between the initial wait and the current ceiling. After `breaker-threshold` failures in a row the circuit opens: no attempts for `breaker-cooldown-s`, then one trial. The socket file appearing forces a trial at once. A read left unanswered for `request-timeout-s` counts as a failure and drops the connection; writes waiting for your approval never time out on tawk-mcp's side (tawk gives up after 2 minutes). A tool call made while tawk is not connected waits at most 2 seconds for a connection in progress.

## Docker

The image sets:

| Variable | Value | Why |
| --- | --- | --- |
| `TAWKMCP_TRANSPORT` | `Http` | The default already, set so the image never depends on it |
| `TAWKMCP_BIND` | `0.0.0.0` | Inside the container; publish the port on `127.0.0.1` only |
| `TAWKMCP_PORT` | `8765` | |
| `TAWKMCP_TOKEN_FILE` | `/data/token` | `/data` is a volume; mount `~/.config/tawk-mcp` there to share the token with the host |
| `TAWK_CONTROL_SOCKET` | `/run/tawk/control.sock` | Mount `$XDG_RUNTIME_DIR/tawk` at `/run/tawk` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8765` | For tooling that reads it; tawk-mcp itself uses `TAWKMCP_BIND` and `TAWKMCP_PORT` |

Run it with `--user "$(id -u):$(id -g)"` and `-p 127.0.0.1:8765:8765`. `TAWKMCP_TOKEN` can be passed instead of the token file, for example from a secret store. The other variables work in the container as anywhere else.

## MCP protocol revision

tawk-mcp uses the initialize handshake, and clients settle on revision 2025-11-25 or on an older revision they ask for. Revision 2026-07-28 starts with `server/discover` and replaces `resources/subscribe` with `subscriptions/listen`, and Claude Code does not register a channel server that negotiates it. tawk-mcp therefore answers `server/discover` as an unknown method, and clients that try it fall back to initialize.
