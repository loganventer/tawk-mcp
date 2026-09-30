# Intent

## Purpose

tawk-mcp exists so that an AI model working for you, through an MCP client such as Claude Code, can help with WhatsApp the way you already use it in tawk: tell you what you missed, find things in your chats, draft replies, and, only when you say so, act. You stay in control. tawk decides what a client may see and do, every write waits for your approval in tawk, and anything destructive needs a second yes that the model cannot give.

## Scope

In scope:

- Every operation of tawk's control protocol ([CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md)) that a model can use safely: reading chats, messages, statuses and scheduled messages; sending, reacting, scheduling, drafting and marking as read; and managing messages, chats, statuses, the profile, settings and the app when tawk allows it.
- The two-step confirmation for destructive operations, with the user asked directly through MCP elicitation.
- Live updates: MCP resource subscriptions, Claude Code channel events, and a server-sent event stream for other programs.
- Prompts that make common tasks one step: catching up and drafting a reply.
- Streamable HTTP by default and stdio as the alternative, a systemd user service, and a Docker image, for Linux and macOS, with Windows through WSL.
- Starting and running whether or not tawk is running, and recovering by itself when tawk comes and goes.

## Out of scope

- Talking to WhatsApp directly. tawk-mcp only ever talks to a running tawk.
- Storing messages. tawk-mcp keeps no copy of your chats, no cache and no log file.
- Deciding what a client may do. Access, visible chats and rate limits live in tawk's settings, and tawk-mcp cannot change them.
- Unlocking locked chats, logging out, encryption, backups, Automation settings, settings that run programs, and folders and files: tawk keeps these out of reach, and tawk-mcp offers no way to them.
- Confirming anything on the user's behalf. There is no confirm tool.
- Choosing or calling a model. tawk-mcp serves MCP; which model reads the results is up to your client.

## Boundary rules

- **tawk is the authority.** tawk-mcp never widens what tawk allows, never works around a refusal, and says origin `mcp` in its hello so tawk treats it as a model.
- **Other people's text is data.** Everything written by others reaches the model fenced as untrusted, in tool results, resources, prompts and channel events.
- **Confirmation tokens stay inside tawk-mcp.** They never appear in a tool result, a log line, a notification or an error.
- **Nothing leaves the machine except what your MCP client sends to its model service.** tawk-mcp has no telemetry and no update checks, and in HTTP mode it listens on loopback unless told otherwise.
- **Never hang.** A tool call gets an answer within seconds whether tawk is up, down, restarting or hung, except while you are deciding on an approval.
- **One way to build a thing.** iDesign layers, one type per file, interfaces bound only in the composition root, and warnings as errors.

## Relationship to tawk

tawk-mcp is a separate program that speaks tawk's control protocol version 1. The protocol is owned by tawk and documented in its CONTROL.md; when tawk adds operations, tawk-mcp follows. tawk's own `tawk send`, `tawk tail`, `tawk unread` and `tawk status-line` commands use the same socket for your shell; tawk-mcp is the way in for models.
