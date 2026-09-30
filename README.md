# tawk-mcp

tawk-mcp is the MCP server for [tawk](https://github.com/loganventer/tawk), a WhatsApp client for the terminal.

It lets MCP clients such as Claude Code, Claude Desktop and VS Code read your WhatsApp chats through a running tawk and, when you allow it in tawk, send messages and manage tawk for you. Every send waits for your approval in tawk, and anything destructive needs two separate yeses from you, one of which the model can never give.

tawk-mcp talks to tawk over tawk's control socket, described in tawk's [CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md). Turning the socket on is covered in the [Automation and MCP](https://github.com/loganventer/tawk/blob/main/MANUAL.md#automation-and-mcp) section of tawk's manual.

## Features

- **Read your chats**: list chats, read and search messages, see what is unread, chat details, statuses and scheduled messages. Reading never marks anything as read.
- **Write with your approval**: send, react, schedule, mark as read, or put a draft into tawk's input box for you to finish yourself.
- **Manage tawk** (with `access = manage`): edit, delete and forward messages, mute, pin, archive and export chats, statuses, your profile and tawk's settings.
- **Two-step confirmation** for destructive operations, asked of you directly in your MCP client and again in tawk.
- **Live updates**: resource subscriptions, Claude Code channel events, and a server-sent event stream at `/events`.
- **Prompts**: `catch_up` and `draft_reply`.
- **Resilient**: starts without tawk, connects as soon as tawk's socket appears, reconnects after tawk restarts, with backoff and a circuit breaker.
- **Streamable HTTP by default**, on loopback with a bearer token; stdio when a client should start it; a Docker image; the installer registers it to start at boot.
- **Untrusted text is fenced**: message text reaches the model inside clearly marked blocks it is told not to obey.

## Install

With one command:

```sh
curl -fsSL https://raw.githubusercontent.com/loganventer/tawk-mcp/main/install.sh | bash
```

The installer builds tawk-mcp from source as one self-contained file in `~/.local/bin`. It finds the .NET 10 SDK, or offers to install it into `~/.dotnet` with Microsoft's `dotnet-install.sh` (no sudo). It registers tawk-mcp to start at boot (a systemd user service with linger on Linux, a LaunchAgent on macOS, or an `@reboot` crontab line where there is no systemd), starts it, creates the bearer token and prints the exact `claude mcp add` line. From a checkout, run `./install.sh` instead.

| Goal | Command |
| --- | --- |
| Install without starting it at boot | `./install.sh --no-service` |
| Build the Docker image instead | `./install.sh --docker` |
| Install somewhere else | `./install.sh --prefix /opt/tawk-mcp` |
| Build a tag or branch | `./install.sh --ref v0.1.0` |
| Install the SDK without asking | `./install.sh --yes` |
| Remove tawk-mcp | `./install.sh --uninstall` |

Piped from GitHub, pass options after `bash -s --`, for example `curl -fsSL https://raw.githubusercontent.com/loganventer/tawk-mcp/main/install.sh | bash -s -- --no-service`. Release binaries for Linux (x64, arm64) and macOS (Intel, Apple silicon) are also on the [releases page](https://github.com/loganventer/tawk-mcp/releases).

tawk-mcp needs [tawk](https://github.com/loganventer/tawk) running beside it; the installer tells you if tawk is missing. In tawk, turn on **Settings > Automation > Control socket** and choose what agents may do under **Settings > Automation > Agent access**. What agents do shows in tawk's Agentic tab (click **🤖 Agentic** in the header, or press F3). [QUICKSTART.md](QUICKSTART.md) walks through it in five minutes.

## Quick setup

tawk-mcp serves MCP over streamable HTTP at `http://127.0.0.1:8765/mcp`. The installer starts it and registers it to start at boot ([MANUAL.md](MANUAL.md#keeping-it-running)); with `--no-service`, start it yourself:

```sh
tawk-mcp                            # http://127.0.0.1:8765/mcp
tawk-mcp print-token                # the bearer token every request needs
```

**Claude Code**

```sh
claude mcp add --transport http tawk http://127.0.0.1:8765/mcp \
  --header "Authorization: Bearer $(tawk-mcp print-token)"
```

**Claude Desktop** (`claude_desktop_config.json`), through the `mcp-remote` bridge because the file only starts local programs:

```json
{
  "mcpServers": {
    "tawk": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "http://127.0.0.1:8765/mcp", "--header", "Authorization: Bearer YOUR_TOKEN"]
    }
  }
}
```

**VS Code** (`.vscode/mcp.json`)

```json
{
  "inputs": [{ "type": "promptString", "id": "tawk-token", "description": "tawk-mcp token", "password": true }],
  "servers": {
    "tawk": {
      "type": "http",
      "url": "http://127.0.0.1:8765/mcp",
      "headers": { "Authorization": "Bearer ${input:tawk-token}" }
    }
  }
}
```

**Docker**

```sh
scripts/docker-run.sh               # builds, runs as you, prints the token and a client snippet
```

**stdio instead.** A client can also start tawk-mcp itself with `--stdio`, for example `claude mcp add tawk -- tawk-mcp --stdio`. Claude Code channels, which push new messages into a session, need this; see [MANUAL.md](MANUAL.md#claude-code-channel).

**Windows**: run tawk and tawk-mcp under WSL, and connect over HTTP through WSL2's localhost forwarding. See [MANUAL.md](MANUAL.md#windows).

## Tools

| Area | Read | Write |
| --- | --- | --- |
| Chats | `list_chats`, `read_messages`, `search_messages`, `unread_summary`, `get_chat_info` | `set_chat`, `set_chat_theme`, `clear_chat`, `delete_chat`, `export_chat`, `block`, `unblock` |
| Messages | | `draft_message`, `send_message`, `react`, `mark_read`, `edit_message`, `delete_message`, `forward_message`, `retry_message`, `download_media` |
| Scheduled messages | `list_scheduled` | `schedule_message`, `cancel_scheduled`, `reschedule`, `send_scheduled_now` |
| Statuses | `list_statuses`, `status_viewers`, `list_backgrounds` | `post_status`, `reply_status`, `like_status` |
| Profile | `get_profile` | `set_profile`, `set_profile_photo`, `remove_profile_photo` |
| Settings | `get_settings`, `list_themes` | `set_setting` |
| tawk itself | `app_status` | `reconnect`, `decline_call` |

Resources: `tawk://chats` and `tawk://chat/{jid}`. Prompts: `catch_up` and `draft_reply`. Every tool is described in [MANUAL.md](MANUAL.md).

## Documentation

| Document | What it covers |
| --- | --- |
| [QUICKSTART.md](QUICKSTART.md) | From nothing to asking "what did I miss?" in five minutes |
| [MANUAL.md](MANUAL.md) | Every tool, resource and prompt, notifications, confirmations and troubleshooting |
| [CONFIGURATION.md](CONFIGURATION.md) | Every environment variable and argument, and the tawk settings that matter |
| [ARCHITECTURE.md](ARCHITECTURE.md) | The layers and components |
| [HOW_IT_WORKS.md](HOW_IT_WORKS.md) | What happens on a read, a send, a destructive request, a reconnect |
| [INTENT.md](INTENT.md) | Purpose, scope and boundary rules |
| [SECURITY.md](SECURITY.md) | The threat model and how to report a problem |
| [AGENT.md](AGENT.md) | Notes for coding agents working on this repository |

## Licence

MIT, by Logan Venter. See [LICENSE](LICENSE).
