# Quickstart

Five minutes from nothing to asking your MCP client "what did I miss?". This uses Claude Code; the other clients are in [README.md](README.md#quick-setup).

## 1. Have tawk running and linked

Install [tawk](https://github.com/loganventer/tawk) and link it to your phone as its quickstart describes:

```sh
curl -fsSL https://raw.githubusercontent.com/loganventer/tawk/main/install.sh | bash
```

Leave it running in a terminal.

## 2. Turn on the control socket

In tawk open **Settings > Automation** and turn on **Control socket**. Leave **Agent access** on `read` for now: the model can then read and nothing else. Whatever agents do later shows in tawk's Agentic tab (click **🤖 Agentic** in the header, or press F3).

The same in `~/.config/tawk/config.ini`:

```ini
[automation]
control_socket = on
access = read
```

## 3. Install tawk-mcp

```sh
curl -fsSL https://raw.githubusercontent.com/loganventer/tawk-mcp/main/install.sh | bash
```

The installer builds tawk-mcp into `~/.local/bin`. If the .NET 10 SDK is missing it offers to install it into `~/.dotnet` first (no sudo). It registers tawk-mcp to start at boot and starts it (a systemd user service on Linux, a LaunchAgent on macOS), then creates the bearer token and prints the `claude mcp add` line to use. From a checkout, `./install.sh` does the same.

## 4. Start it and add it to Claude Code

tawk-mcp is already running and serving MCP over HTTP on your own machine (check with `curl http://127.0.0.1:8765/healthz`). If the installer said it could not register it, start it yourself in a spare terminal with `tawk-mcp`.

Then add it to Claude Code:

```sh
claude mcp add --transport http tawk http://127.0.0.1:8765/mcp \
  --header "Authorization: Bearer $(tawk-mcp print-token)"
```

Start `claude` and run `/mcp`. `tawk` should show as connected with its tools.

## 5. Ask

```text
What did I miss on WhatsApp today?
```

The model calls `unread_summary`, then `read_messages` for the chats that need it, and summarises. You can also run the prompt directly with `/mcp__tawk__catch_up`.

Nothing is marked as read. To let the model propose replies, set **Access** to `send` in tawk: it can then put a draft into a chat's input box with `draft_message` for you to edit and send, or send a message, which tawk shows you to approve first.

## If it does not work

| You see | Do this |
| --- | --- |
| "tawk is not running, or its control socket is off" | Start tawk and turn on the control socket. tawk-mcp connects by itself as soon as the socket appears. |
| "tawk does not allow this" | The action needs `access = send` or `manage` in tawk, or the chat is outside tawk's `chats` list. |
| The tools do not show in `/mcp` | Check tawk-mcp is running (`curl http://127.0.0.1:8765/healthz`) and that `claude mcp list` shows `tawk`. |
| 401 from tawk-mcp | The token in the header is wrong or missing. Add the server again with `$(tawk-mcp print-token)`. |

More in [MANUAL.md](MANUAL.md#troubleshooting).
