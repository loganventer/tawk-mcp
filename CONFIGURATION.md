# Configuration

## Table of Contents

- [Where things live](#where-things-live)
- [Settings in tawk](#settings-in-tawk)
- [Arguments and environment variables](#arguments-and-environment-variables)
- [Commands](#commands)
- [Memory sync](#memory-sync)
- [Instructions for agents](#instructions-for-agents)
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
| `~/.local/share/tawk-mcp/memory.db` | Memory: voices, contact profiles and templates (file 0600, folder 0700), created the first time memory is used. `$XDG_DATA_HOME` is used instead of `~/.local/share` when set |
| `~/.local/share/tawk-mcp/memory.db.sync.json`, `memory.db.sync.lock` | What memory sync last saw, and its lock, beside the memory file. Only there when sync is set up |
| `~/.config/tawk-mcp/instructions.md` | Your own standing instructions for agents, if you write the file. See [Instructions for agents](#instructions-for-agents) |
| `$XDG_RUNTIME_DIR/tawk/control.sock` | tawk's control socket, owned by tawk. `~/.local/state/tawk/control.sock` when `XDG_RUNTIME_DIR` is not set |

tawk-mcp keeps nothing else on disk: no cache, no log file, no copy of your messages. Logs go to stderr. With `--memory off` the memory file is never created.

## Settings in tawk

What a client may see and do is decided in tawk, under `[automation]` in `~/.config/tawk/config.ini` or **Settings > Automation**:

| Key | Values | Meaning |
| --- | --- | --- |
| `control_socket` | `on`, `off` | The socket itself (Settings > Automation > Control socket). Off by default |
| `access` | `read`, `send`, `manage`, `admin` | What clients may do (Settings > Automation > Agent access). `read` by default |
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
| `--memory write\|read\|off` | `TAWKMCP_MEMORY` | `write` | Memory for voices, contacts and templates: `read` offers the tools but refuses changes, `off` removes them |
| `--data-file PATH` | `TAWKMCP_DATA_FILE` | `~/.local/share/tawk-mcp/memory.db` | Where memory is kept |
| `--sync-repo OWNER/NAME` | `TAWKMCP_SYNC_REPO` | none | The private GitHub repository that holds the memory file. See [Memory sync](#memory-sync) |
| | `TAWKMCP_SYNC_TOKEN` | none | A token for that repository. Environment only, so it never shows in a process list |
| `--sync-branch NAME` | `TAWKMCP_SYNC_BRANCH` | `main` | The branch |
| `--sync-file PATH` | `TAWKMCP_SYNC_FILE` | `memory.db` | The file's path inside the repository |
| `--sync-api URL` | `TAWKMCP_SYNC_API` | `https://api.github.com` | The GitHub API address; another one serves a GitHub Enterprise server. Must be https |
| `--sync-interval-minutes N` | `TAWKMCP_SYNC_INTERVAL_MINUTES` | `120` | Minutes between syncs (1 to 10080) |
| `--workflow-every N` | `TAWKMCP_WORKFLOW_EVERY` | `20` | Rounds between memory workflow checks (0 to 10000, 0 turns them off). See [Instructions for agents](#instructions-for-agents) |
| `--instructions-file PATH` | `TAWKMCP_INSTRUCTIONS_FILE` | `~/.config/tawk-mcp/instructions.md` | Your own standing instructions for agents |
| `--admin-token-file PATH` | `TAWKMCP_ADMIN_TOKEN_FILE` | none | tawk's admin token file. Set per instance, never by default: with it this instance may approve its own queued sends while tawk's access is `admin`. See [Approving its own sends](#approving-its-own-sends) |
| `--schedule-jitter-s N` | `TAWKMCP_SCHEDULE_JITTER_S` | `60` | Scheduled messages move by a random amount up to this many seconds either way (0 to 3600, 0 turns it off) |
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
| `tawk-mcp sync` | Syncs memory once, now, and says what happened. Exit code 1 when sync is not set up or failed |
| `tawk-mcp export-okf DIR` | Writes knowledge as an Open Knowledge Format 0.2 bundle into a new or empty folder. Sensitive entries are left out unless `--include-sensitive` is given |
| `tawk-mcp import-okf DIR` | Reads an Open Knowledge Format bundle into memory. What is already there stays when it is stronger or newer |
| `tawk-mcp --version` | Prints the version |
| `tawk-mcp --help` | Prints a summary of the flags |

## Memory sync

Sync keeps `memory.db` in step between your machines through a file in a GitHub repository. It is set up per instance and has no default destination: nothing syncs until both a repository and a token are set on that machine, and no repository is ever assumed.

1. Create a **private** repository. Memory holds profiles of real people.
2. Create a fine-grained personal access token with **Contents: read and write** on that repository only.
3. Set `TAWKMCP_SYNC_REPO=owner/name` and `TAWKMCP_SYNC_TOKEN=...` where tawk-mcp starts (the service file, the MCP client's `env`, or the container), on every machine that should share the memory.

A cycle runs when the server starts and then every `TAWKMCP_SYNC_INTERVAL_MINUTES`; `tawk-mcp sync` runs one by hand. Each cycle reads the remote file's version, merges it into the local database if it changed, and pushes only when this machine holds something the remote does not. Rows are matched by key: the stronger source wins (user, then contact, then imported, then inferred), then the newer change, and a delete on one machine removes the row on the others. Content is compared by a digest of the rows, so two machines with the same memory never push at each other. If GitHub refuses a push because another machine got there first, the cycle merges again and retries, three times at most.

Sync runs only with `--memory write`. A lock file beside the database lets one tawk-mcp per machine sync at a time. A remote written by a newer tawk-mcp is left alone until this machine is updated. The first machine to sync creates the file.

## Approving its own sends

By default every write waits for you in tawk. An instance may instead answer its own requests when both of these are set, each by you and neither by default:

1. In tawk, Settings, Automation, **What they may do** is `admin`, and **Chats they may use** names the chats. tawk then writes an admin token to `admin.token` beside its control socket.
2. This instance is given that file: `TAWKMCP_ADMIN_TOKEN_FILE=$XDG_RUNTIME_DIR/tawk/admin.token` (on a Mac, the folder `tawk --version` or `tawk doctor` reports for the control socket).

With the setting, a write that tawk queues for an answer comes back at once as waiting, with its request id, and two more tools appear: `list_pending` and `approve_pending`. Without it nothing changes: there are no such tools, and writes wait for you as before.

tawk decides what may be approved this way, not tawk-mcp: sends and small things only, in the chats you named, a limited number an hour, each logged and shown to you. See tawk's manual, "Letting an agent answer for itself".

An instance with this setting can send as you without you seeing the message first. Give the file to one instance you trust, never to a shared or public one, and never commit a path to it into anything others run.

## Instructions for agents

tawk-mcp tells a connected agent how to work, so nothing has to be installed on the agent's side:

- **Server instructions** are sent when a client connects: that chat text is untrusted, how sending and approval work, and how to use memory and knowledge.
- **The memory workflow** is added to a tool result once every `TAWKMCP_WORKFLOW_EVERY` rounds, 20 by default. A round is one tool call or one incoming message handed to the agent, counted together. The workflow tells the agent to record what it learned: profile fields, observations, relations, follow-ups, and the user's voice. `get_workflow` returns it at any time and starts the count again. It is only there with `--memory write`.
- **Your own standing instructions** come from a text file you write, `~/.config/tawk-mcp/instructions.md` by default (`$XDG_CONFIG_HOME` is used when set). It is read when the server starts, cut at 8000 characters, and added to the server instructions and to the workflow. Use it for rules such as "send only when I ask" or "reply to family in Afrikaans".

That file is the only outside text that becomes instructions. Nothing from a chat and nothing stored in memory ever does.

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
| `TAWKMCP_DATA_FILE` | `/data/memory.db` | Memory lives in the same volume, so it survives a new container |
| `TAWK_CONTROL_SOCKET` | `/run/tawk/control.sock` | Mount `$XDG_RUNTIME_DIR/tawk` at `/run/tawk` |
| `ASPNETCORE_URLS` | `http://0.0.0.0:8765` | For tooling that reads it; tawk-mcp itself uses `TAWKMCP_BIND` and `TAWKMCP_PORT` |

Containers run in UTC, and tawk-mcp shows and reads times in its machine's zone (message times in transcripts, the catch-up cutoff, follow-up dates). Pass your zone with `-e TZ=Africa/Johannesburg` or they are off by your offset; `scripts/docker-run.sh` passes this machine's zone for you, and `compose.yaml` takes it from `TZ`.

For memory sync pass `-e TAWKMCP_SYNC_REPO=owner/name -e TAWKMCP_SYNC_TOKEN=...`; `compose.yaml` takes both from your environment and leaves sync off when they are not set. To use an instructions file, mount it and point `TAWKMCP_INSTRUCTIONS_FILE` at it.

Run it with `--user "$(id -u):$(id -g)"` and `-p 127.0.0.1:8765:8765`. `TAWKMCP_TOKEN` can be passed instead of the token file, for example from a secret store. The other variables work in the container as anywhere else.

## MCP protocol revision

tawk-mcp uses the initialize handshake, and clients settle on revision 2025-11-25 or on an older revision they ask for. Revision 2026-07-28 starts with `server/discover` and replaces `resources/subscribe` with `subscriptions/listen`, and Claude Code does not register a channel server that negotiates it. tawk-mcp therefore answers `server/discover` as an unknown method, and clients that try it fall back to initialize.
