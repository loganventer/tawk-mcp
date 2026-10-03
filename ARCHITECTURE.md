# Architecture

## Table of Contents

- [Overview](#overview)
- [Layers](#layers)
- [Component map](#component-map)
- [Components](#components)
- [Composition root](#composition-root)
- [Concurrency](#concurrency)
- [Design principles](#design-principles)
- [Adding things](#adding-things)

## Overview

tawk-mcp is a .NET 10 program built on the official MCP C# SDK (`ModelContextProtocol` and `ModelContextProtocol.AspNetCore` 2.2.0), arranged in [iDesign](https://www.idesign.net/) layers. The volatile parts (tawk's socket protocol, the MCP transport, the model-facing text) sit behind interfaces, so each can change without touching the rest. Every interface is bound to its implementation in exactly one place, `TawkMcpComposition` in the host.

Each class, record, enum and interface lives in its own file. Nullable reference types, the .NET analysers and warnings as errors are on for every project (`Directory.Build.props`).

## Layers

| Layer | Project | Responsibility | May call |
| --- | --- | --- | --- |
| Host | `Tawk.Mcp.Host` (assembly `tawk-mcp`) | The composition root, the command line, streamable HTTP hosting (the default) and stdio hosting, the bearer token and origin middleware, `/events` and `/healthz` | Everything, to compose it |
| Clients | `Tawk.Mcp.Clients` | MCP tools, resources and prompts, resource subscriptions, the channel sink, the event stream hub, session tracking, the notification dispatcher | Managers |
| Managers | `Tawk.Mcp.Managers` | Use cases: reading, sending, managing messages, chats, scheduled messages, statuses, the profile, settings and the app, and live updates; in `Memory`, categories, voices, contact profiles and templates | Engines, resource access |
| Engines | `Tawk.Mcp.Engines` | Rules with no I/O: transcript and chat formatting, untrusted text fencing, catch-up and draft planning, notification headers, exponential backoff, the circuit breaker, schedule jitter; in `Memory`, style measuring, voice rules and scoring, voice resolution, guide import, the contact field catalog, template filling and memory formatting | Core |
| Resource access | `Tawk.Mcp.ResourceAccess` | tawk's control socket: the line codec, the connection, the control client, the connection supervisor, the socket locator and watcher, the confirmation gate, the chat source for memory; in `Memory`, the SQLite connection factory, schema migrator and one store per kind of memory | Core |
| Core | `Tawk.Mcp.Core` | Records for tawk's values, events, error codes, the `TawkControlException`, and contracts shared across layers (`ICircuitBreaker`, `IBackoffPolicy`, `IUserConfirmation`, `IDelay`) | Nothing above |

```mermaid
flowchart TD
    HOST["Host<br/>TawkMcpComposition, Program,<br/>BearerTokenMiddleware, OriginGuardMiddleware,<br/>EventStreamEndpoint, HealthEndpoint"]
    CL["Clients<br/>ChatTools, MessageTools, ScheduleTools, StatusTools,<br/>ProfileTools, SettingsTools, AppTools,<br/>CategoryTools, VoiceTools, ContactTools, TemplateTools,<br/>ChatResources, MemoryResources, TawkPrompts,<br/>ResourceUpdatePump, ChannelEventSink, EventStreamHub,<br/>NotificationDispatcher"]
    MG["Managers<br/>ChatReadingManager, MessageSendingManager,<br/>MessageManagementManager, ChatManagementManager,<br/>ScheduleManagementManager, StatusManager,<br/>ProfileManager, SettingsManager, AppManager,<br/>LiveUpdatesManager, CategoryManager, VoiceManager,<br/>ContactProfileManager, TemplateManager"]
    EN["Engines<br/>TranscriptFormatter, ChatDirectoryFormatter,<br/>UntrustedTextFence, CatchUpPlanner, DraftReplyPlanner,<br/>NotificationFormatter, ExponentialBackoffPolicy, CircuitBreaker,<br/>RandomScheduleJitter, VoiceChecker and its rules,<br/>VoiceResolver, ContactFieldCatalog, TemplateRenderer"]
    RA["Resource access<br/>UnixSocketTawkControl, ControlConnection,<br/>ControlLineCodec, TawkConnectionSupervisor,<br/>ConfirmationGate, SocketFileWatcher, ControlSocketLocator,<br/>TawkChatSource, SqliteConnectionFactory,<br/>Sqlite category, voice, contact and template stores"]
    CO["Core<br/>ChatSummary, ChatMessage, TawkEvent, ControlError,<br/>ICircuitBreaker, IBackoffPolicy, IUserConfirmation"]
    TAWK[("tawk<br/>control.sock")]
    MCP(["MCP clients<br/>HTTP or stdio"])

    HOST -.->|creates and injects| CL & MG & EN & RA
    MCP --> HOST
    HOST --> CL
    CL --> MG
    MG --> EN
    MG --> RA
    MG -.->|IEventSink| CL
    RA -.->|ICircuitBreaker, IBackoffPolicy| EN
    RA -.->|IUserConfirmation| CL
    RA --> TAWK
    RA --> DB[("memory.db")]
    EN --> CO
    RA --> CO
    MG --> CO
    CL --> CO
```

Solid arrows are calls; dotted arrows are construction and calls through an interface owned by a lower layer. Calls flow downwards. Managers never call each other; tools compose them where a use case needs two (the chat tools use the reading and the chat management managers). Three calls go the other way, each through a contract in a lower layer so no project references one above it:

- The live updates manager hands updates to `IEventSink` implementations, which are clients.
- The connection supervisor and control client use `ICircuitBreaker` and `IBackoffPolicy` from core; the engines implement them.
- The confirmation gate asks the user through `IUserConfirmation` from core; the clients implement it with MCP elicitation.

## Component map

```mermaid
flowchart LR
    subgraph Clients
        TOOLS["Tools"]
        RES["ChatResources"]
        PR["TawkPrompts"]
        SUBH["ResourceSubscriptionHandlers"]
        REG["ResourceSubscriptionRegistry"]
        PUMP["ResourceUpdatePump"]
        CH["ChannelEventSink"]
        HUB["EventStreamHub"]
        SESS["ClientSessionRegistry"]
        DISP["NotificationDispatcher"]
        ELIC["ElicitationConfirmation"]
    end
    subgraph Managers
        READ["ChatReadingManager"]
        SEND["MessageSendingManager"]
        MANAGE["Management managers"]
        APPROVE["ApprovalManager"]
        LIVE["LiveUpdatesManager"]
    end
    subgraph ResourceAccess
        GATE["ConfirmationGate"]
        PARK["ParkedRequests"]
        TOKEN["FileAdminTokenSource"]
        CTRL["UnixSocketTawkControl"]
        CONN["ControlConnection"]
        SUP["TawkConnectionSupervisor"]
        WATCH["SocketFileWatcher"]
    end
    TOOLS --> READ & SEND & MANAGE & APPROVE
    TOOLS -.-> ELIC
    RES & PR --> READ
    SUBH --> REG
    DISP --> LIVE
    LIVE --> CTRL
    LIVE --> PUMP & CH & HUB
    PUMP --> REG
    CH --> SESS
    READ & SEND --> CTRL
    MANAGE --> GATE --> CTRL
    GATE -.-> ELIC
    SUP --> CTRL
    SUP --> WATCH
    APPROVE --> CTRL
    APPROVE --> TOKEN
    CTRL --> PARK
    CTRL --> CONN
```

## Components

### Core

Records mirror tawk's values in [CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md): `ChatSummary`, `ChatMessage` (with `ReplyRef` and `LinkCard`), `StatusItem`, `ScheduledItem`, `UnreadSummary`, `ChatInfo` and `GroupMember`, `HelloInfo`, and result records such as `MessagePage`, `SentMessage` and `ScheduledMessage`. They are read with `System.Text.Json` and a snake_case naming policy, and unknown fields are ignored, as the protocol requires.

`TawkEvent` is the root of the notifications: `MessageEvent`, `ChatUpdatedEvent`, `ByeEvent`, `ApprovalEvent`, `UnknownEvent`, and `ConnectionStateEvent`, which tawk-mcp raises itself when its connection changes. `ControlError` carries tawk's error (`code`, `message`, `candidates`, `retry_after`), `ControlErrorCode` names the codes, and `TawkControlException` is how every failure travels upwards.

### Resource access

- `ControlLineCodec` writes requests as one JSON line (`{"id","op","args"}`) and reads each incoming line as an answer or a notification.
- `ControlConnection` owns one socket. A single reader loop matches answers to waiting requests by id through a `TaskCompletionSource` per request, so answers may arrive in any order. It passes approval notifications to the request they belong to, times out reads (writes are exempt), and fails everything still waiting when the connection ends.
- `UnixSocketTawkControl` implements `ITawkControl` for the managers and `ITawkConnector` for the supervisor. A request uses the current connection, waits up to 2 seconds for one in progress, or fails at once while the circuit is open. It publishes events to every reader through `EventBroadcaster`, which also hands the latest connection state to a reader that starts late.
- `TawkConnectionSupervisor` is a hosted service that keeps connecting: backoff between attempts, the circuit breaker around them, and an immediate attempt when `SocketFileWatcher` sees `control.sock` appear or a request is waiting (`ConnectSignal`).
- `ConfirmationGate` runs writes. When tawk answers `needs_confirmation`, it keeps the token in a local variable, asks the user through `IUserConfirmation`, and calls `confirm` or `cancel_confirmation`. The token never leaves it.
- `UnixSocketTawkControl` also implements `ITawkApprovals`. With `ParkWaitingWrites` on (an admin token file is configured), a write that tawk queues for an answer is kept in `ParkedRequests` under its request id and its caller gets `ApprovalWaitingException` instead of waiting; `ApproveAsync` sends `approve` with the admin token and returns the parked request's own answer. `FileAdminTokenSource` (`IAdminTokenSource`) reads the token from tawk's file each time, since tawk replaces it.
- `ControlSocketLocator` finds the socket (flag, `TAWK_CONTROL_SOCKET`, `$XDG_RUNTIME_DIR`, `~/.local/state`).
- `TawkChatSource` (`ITawkChatSource`) is what memory needs from tawk: which chat a name means, through `chat_info` so tawk's visibility rules apply, and the text of the user's own recent messages for `learn_voice`.
- In `Memory`, `SqliteConnectionFactory` owns the database path, creates the file 0600 in a 0700 folder on first use, turns on WAL and runs `SqliteSchemaMigrator`, which applies numbered steps recorded in `PRAGMA user_version`. `ICategoryStore`, `IVoiceStore`, `IContactStore`, `ITemplateStore` and `IOkfStore` are the repositories: each persists one kind of memory with parameterised SQL through `Microsoft.Data.Sqlite`, and no SQL exists outside them. `IOkfStore` holds Open Knowledge Format concepts and links; `SqliteContactStore` keeps each contact's concept in step and serves notes from observation concepts. Every delete writes a row to `sync_tombstone` through `Tombstones`, and every write clears it. `IOkfBundleFiles` reads and writes a bundle folder on disk.
- In `Sync`, `IMemorySnapshotStore` reads every synced row of a database (the local one, or a downloaded copy, which it first brings up to the current schema), applies a merge in one transaction and makes a consistent copy; `SyncTables` lists the tables with their keys and parents. `IMemoryRemote` is the remote file, implemented by `GitHubMemoryRemote` over the REST API. `ISyncStateStore`, `ISyncLock` and `ISyncScratch` keep the last version seen, the per-machine lock and the private temporary copies.

### Engines

- `TranscriptFormatter` writes `[2026-09-30 18:02] Mom: text` lines with reply, media, forward, deletion, link, edit, reaction and status markers, indenting extra lines. `ChatDirectoryFormatter` does the same for chats, chat details and scheduled messages.
- `UntrustedTextFence` wraps other people's text between fixed markers with a statement that it is data, and breaks up any run of angle brackets inside so the text cannot close the fence.
- `CatchUpPlanner` and `DraftReplyPlanner` write the prompt instructions. `NotificationFormatter` writes the one-line header of a channel event.
- `ExponentialBackoffPolicy` and `CircuitBreaker` (closed, open, half-open) take an injected random source and `TimeProvider`. `RandomScheduleJitter` also takes a random source, and turns a schedule time into one shifted by whole seconds.
- In `Memory`, measuring, judging and scoring are separate: `StyleFeatureExtractor` measures a draft, each `IVoiceRule` (forbidden patterns, case, emoji, length, language, address form, required markers, greeting and sign-off, learnt baseline) judges one thing, and `VoiceChecker` runs whichever rules are registered and scores the findings. `VoiceResolver` picks the variant for a category path and overlays its rules. `ContactFieldCatalog` holds `ContactFieldDefinition`s (from `StandardContactFields`) and checks values with one `IFieldValueValidator` per kind of value. `VoiceGuideImporter`, `StyleBaselineCalculator`, `TemplateRenderer` and `MemoryFormatter` do what their names say, and `MemoryWriteGuard` refuses changes in read-only mode.
- In `Knowledge`, `KnowledgeFormatter` writes concepts and observations for a model, `KnowledgePrecedence` decides which of two versions is kept (stronger source, then newer), and `OkfBundleWriter` and `OkfBundleReader` turn concepts into Markdown files with YAML frontmatter and back. Reading uses YamlDotNet; writing is a small emitter that quotes anything YAML could misread.
- In `Sync`, `MemoryMerger` holds the merge rules and `MemoryDigest` fingerprints the content. Both are pure: they see snapshots, never a database.

### Managers

Each manager is one area of use cases and returns model-ready text. The reading and sending managers call `ITawkControl` directly; the management managers go through `IConfirmationGate` so any answer that asks for confirmation is handled the same way. `LiveUpdatesManager` reads the event stream, subscribes to every chat after each connect, fences new messages for the model, and hands every update to each `IEventSink`.

The memory managers (`CategoryManager`, `VoiceManager`, `ContactProfileManager`, `TemplateManager`) call the stores and memory engines, and fence what they return. They share two small helpers rather than calling each other: `CategoryEnsurer` creates a category the first time it is named, and `VoiceSelector` picks the voice and variant for a chat or category. `VoiceManager` also implements `IDraftGuidance` for the `draft_reply` prompt; with memory off, `NoDraftGuidance` takes its place. Deletions ask the user through `IUserConfirmation` and throw `MemoryException` if they do not agree.

`ApprovalManager` lists this instance's waiting requests and approves one with the admin token, turning tawk's refusals into plain text that says the request still waits for the user.

`KnowledgeManager` records and shows observations and relations, with `KnowledgeSubjects` working out which concept a tool argument means. `OkfBundleManager` exports and imports bundles. `MemorySyncManager` runs one sync cycle: lock, read the remote version, merge, compare digests, push, retry on a lost race.

### Clients

- Thirteen tool classes (`[McpServerToolType]`) hold the 76 tools, and `ApprovalTools` adds `list_pending` and `approve_pending` only when an admin token file is configured; the memory tool classes and `MemoryResources` are left out when memory is off, leaving 42. Each tool calls one manager and turns `TawkControlException` into a tool error result through `ControlErrorMessages`, and `MemoryException` into one with its own message, so nothing crashes the call. `draft_template` is the one tool that uses two managers, filling the template and then drafting it. Write tools take `IProgress<ProgressNotificationValue>` and report "Waiting for approval in tawk". Tools that may need confirmation take the request's `McpServer` and build an `ElicitationConfirmation` from it.
- `ChatResources` (`[McpServerResourceType]`) serves `tawk://chats` and `tawk://chat/{jid}`; `ResourceSubscriptionHandlers`, `ResourceSubscriptionRegistry` and `ResourceUpdatePump` handle subscriptions.
- `TawkPrompts` (`[McpServerPromptType]`) serves `catch_up` and `draft_reply`.
- `ChannelEventSink` sends `notifications/claude/channel`; `EventStreamHub` keeps the last 200 events for `/events`; `ClientSessionRegistry` and `SessionTracking` remember connected sessions; `ProtocolRevisionFilter` keeps clients on the initialize handshake.
- `NotificationDispatcher` is the hosted service that runs the live updates manager. `MemorySyncService` is the one that runs a sync cycle at start and then every interval; it is registered only when a repository and a token are set.
- In `Workflow`, `WorkflowCadence` counts rounds, `WorkflowReminder` is the tool call filter that adds `TawkWorkflow`'s fixed text to a result when it is due, and `ChannelEventSink` counts each message it delivers. `TawkServerInstructions` holds the server instructions.

### Host

`Program` reads options (`TawkMcpOptionsBinder`: environment, then flags), runs a command (`print-token`, `healthcheck`, `sync`, `export-okf`, `import-okf`, `--version`, `--help`) or serves. The memory commands go through `MemoryCommand`, which composes only the memory services. `UserInstructionsFile` reads the user's own instructions once at start. By default it uses `WebApplication` with `WithHttpTransport` in stateful session mode (sessions carry elicitation, subscriptions and notifications), binds Kestrel where `TAWKMCP_BIND` says, puts `OriginGuardMiddleware` and `BearerTokenMiddleware` in front, and maps `/mcp`, `/events` and `/healthz`. With `--stdio` it uses the generic host with `WithStdioServerTransport` and logs to stderr only. `FileTokenStore` and `FixedTokenStore` implement `ITokenStore`.

## Composition root

`TawkMcpComposition.AddTawkMcp` registers every implementation against its interface, the two hosted services, and the MCP server: server info and instructions, the `claude/channel` experimental capability (unless the channel is off), the tool, resource and prompt types, the subscribe and unsubscribe handlers, and two incoming message filters. Nothing else in the solution creates a service.

## Concurrency

- One reader task per connection reads tawk's socket; writes to the socket are serialised by a semaphore. Any number of requests may be in flight.
- The supervisor runs on its own background task and waits on a delay, a signal, or the end of the connection.
- Every reader of `ITawkControl.Events` gets its own unbounded channel. The live updates manager is the one reader in production, and hands each update to the sinks in turn; a failing sink is logged and skipped.
- The event stream hub gives each `/events` subscriber its own bounded channel, which drops its oldest events if the reader stalls, and keeps the replay buffer under a lock.
- The session registry keeps at most 64 sessions; past that the one heard from longest ago is let go with its subscriptions.
- A sync cycle runs on its own background task and holds a lock file, so two tawk-mcp processes on one machine never sync at once.
- Tool calls run on the MCP SDK's request tasks and share the one connection.

## Design principles

- **Dependency inversion.** Every dependency is an interface, bound in the composition root. Tests replace the lowest layer with hand-written fakes and a fake socket server.
- **Composition over inheritance.** Behaviour is assembled from small services. Inheritance is kept to the SDK's `BackgroundService`, the exception type, and the closed families of event and frame records.
- **Separation of concerns.** Formatting and fencing are engines, protocol details are resource access, MCP details are clients, hosting is the host.
- **Untrusted by default.** Anything written by other people is fenced before it reaches a model, in tool results, resources, prompts and channel events.
- **Never hang.** Every wait has a bound except a write waiting for the user, which tawk itself ends after 2 minutes.

## Adding things

- **A tool for a new tawk operation**: add the method to the manager for its area (or a new manager in its own file with its interface), call `ITawkControl` for reads or `IConfirmationGate` for writes, add the tool method to the matching tool class with a description that says what access it needs and that text is untrusted, register any new manager in `TawkMcpComposition`, add the operation to `UnixSocketTawkControl.WriteOps` if it may wait for approval, and add tests with `FakeTawkControl`.
- **A new error code**: add it to `ControlErrorCode` and `ControlErrorCodes`, and give it a message in `ControlErrorMessages`.
- **A new place for live updates to go**: implement `IEventSink` in the clients and register it.
- **A new voice check**: add a class implementing `IVoiceRule` in `Engines/Memory` and register it in `AddMemory`. `VoiceChecker` does not change.
- **A new contact field**: add a `ContactFieldDefinition` to `StandardContactFields`. A new kind of value needs a `FieldKind` and an `IFieldValueValidator` registered in `AddMemory`.
- **A table that should sync**: add it to `SyncTables` with its key and parent, give its store a tombstone on delete and a clear on write, and make sure its rows carry `updated`.
- **A schema change**: add a step to `SqliteSchemaMigrator` that ends by setting the next `user_version`. Never edit a step that has shipped.
