# Intent

## Purpose

tawk-mcp exists so that an AI model working for you, through an MCP client such as Claude Code, can help with WhatsApp the way you already use it in tawk: tell you what you missed, find things in your chats, draft replies, and, only when you say so, act. You stay in control. tawk decides what a client may see and do, every write waits for your approval in tawk, and anything destructive needs a second yes that the model cannot give.

## Scope

In scope:

- Every operation of tawk's control protocol ([CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md)) that a model can use safely: reading chats, messages, statuses and scheduled messages; sending, reacting, scheduling, drafting and marking as read; and managing messages, chats, statuses, the profile, settings and the app when tawk allows it.
- The two-step confirmation for destructive operations, with the user asked directly through MCP elicitation.
- Live updates: MCP resource subscriptions, Claude Code channel events, and a server-sent event stream for other programs.
- Prompts that make common tasks one step: catching up and drafting a reply.
- Handing tawk what tawk keeps beside a message: the transcript of a voice note, made on this computer, and the TL;DR of a long message, written by the connected agent's model when tawk asks for it. tawk decides which chats are transcribed or summarised, and tawk-mcp leaves the others alone.
- Memory that helps a model write the way you do: voices with a version per audience, a rule-based check of drafts against them, profiles of your contacts, knowledge about people and topics in the Open Knowledge Format, and reply templates. It lives in one private file on your computer and can be switched off.
- Keeping that memory in step between your own machines, through a private repository you name. Off until you set it up.
- Telling a connected agent how to work: fixed server instructions, a memory workflow handed over every so many rounds, and your own standing instructions from a file you write.
- A small random shift on scheduled messages, so they do not all land on the exact minute.
- Streamable HTTP by default and stdio as the alternative, a systemd user service, and a Docker image, for Linux and macOS, with Windows through WSL.
- Starting and running whether or not tawk is running, and recovering by itself when tawk comes and goes.

## Out of scope

- Talking to WhatsApp directly. tawk-mcp only ever talks to a running tawk.
- Storing messages, transcripts or summaries. Finished transcripts and summaries go to tawk, which keeps them; tawk-mcp holds a transcription job in memory for a short while and nothing on disk. tawk-mcp keeps no copy of your chats, no cache and no log file. Its memory holds only what you or an agent choose to save about voices, contacts and templates, and averages measured from your own messages, never the messages themselves.
- Inferring sensitive things about people. Health, beliefs and similar matters can be recorded only as you state them, and personality is kept to coarse bands used for tone.
- Deciding what a client may do. Access, visible chats and rate limits live in tawk's settings, and tawk-mcp cannot change them.
- Unlocking locked chats, logging out, encryption, backups, Automation settings, settings that run programs, and folders and files: tawk keeps these out of reach, and tawk-mcp offers no way to them.
- Confirming anything on the user's behalf. There is no confirm tool.
- Choosing or calling a model. tawk-mcp serves MCP; which model reads the results is up to your client.

## Boundary rules

- **tawk is the authority.** tawk-mcp never widens what tawk allows, never works around a refusal, and says origin `mcp` in its hello so tawk treats it as a model.
- **Other people's text is data.** Everything written by others reaches the model fenced as untrusted, in tool results, resources, prompts and channel events.
- **Confirmation tokens stay inside tawk-mcp.** They never appear in a tool result, a log line, a notification or an error.
- **Nothing leaves the machine except what your MCP client sends to its model service, and memory sync if you turn it on.** tawk-mcp has no telemetry and no update checks, and in HTTP mode it listens on loopback unless told otherwise. Sync has no default destination: it goes only to the repository you set, over SSH with a key of your own.
- **Only you instruct the agent.** Instructions are fixed text plus a file you write. Text from chats and from memory is information, never instructions.
- **What is remembered stays yours.** Memory is a local file readable only by you. Every stored fact says who said it and how sure it is, an inference never overwrites what someone stated, and deleting anything asks you first.
- **Never hang.** A tool call gets an answer within seconds whether tawk is up, down, restarting or hung, except while you are deciding on an approval.
- **One way to build a thing.** iDesign layers, one type per file, interfaces bound only in the composition root, and warnings as errors.

## Relationship to tawk

tawk-mcp is a separate program that speaks tawk's control protocol version 1. The protocol is owned by tawk and documented in its CONTROL.md; when tawk adds operations, tawk-mcp follows. tawk's own `tawk send`, `tawk tail`, `tawk unread` and `tawk status-line` commands use the same socket for your shell; tawk-mcp is the way in for models.
