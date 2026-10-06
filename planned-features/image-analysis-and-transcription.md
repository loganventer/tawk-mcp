# Plan: Image Viewing and Voice Transcription in tawk-mcp

Checked against tawk-mcp `d3729ee` and tawk 0.8.1.

## 1. Summary

Two features, both built on one upstream change:

- **Image viewing.** A tool returns a message's picture to the MCP client as an image content block, and the client's own model looks at it. tawk-mcp runs no vision model.
- **Voice transcription.** A tool starts a transcription and returns at once. When the text is ready, tawk-mcp sends a channel event. How the audio is transcribed (engine, model, languages, task) is set by the user, and the agent may choose per call within what the user allowed. One call can ask for several languages and gets one transcription for each, for voice notes that mix languages. When no transcriber is running for tawk-mcp to connect to, it loads one model inside its own process and reuses that single instance for every job.
- **Prerequisite.** tawk must say where a downloaded file is, and say when a download finishes. tawk-mcp does not read tawk's database or guess at its folders.

## 2. Where it sits

```
MCP client (Claude Code, Claude Desktop, VS Code)
        |  MCP over HTTP or stdio, plus channel events
        v
tawk-mcp (.NET 10)
  Clients -> Managers -> Engines -> ResourceAccess
        |                               |
        |  control socket               |  transcriber: a running one
        |  (one JSON object a line)     |  (HTTP endpoint or a command),
        v                               v  else one model in process
tawk (C, ncurses)                 whisper server, faster-whisper, whisper CLI,
                                  or Whisper.net inside tawk-mcp
  tawk.db, media cache, whatsmeow or Baileys backend
```

The control protocol is line-delimited JSON (`{"id","op","args"}`), version 1. It is not JSON-RPC.

## 3. What the code says today

- `control_codec_message` (tawk, `src/clients/control/control_codec.c`) writes `id`, `chat`, `sender`, `type`, `text` and the rest, and no `media_path`. `type` is one of `text`, `image`, `video`, `audio`, `document`, `sticker`, `other`. A voice note is `audio`, with no duration.
- `download_media` answers `{}`. tawk's `on_media_ready` records the path and repaints the screen, and sends nothing over the control socket.
- In tawk-mcp, `ChatMessage` has no path, `MessageManagementManager.DownloadMediaAsync` returns a fixed sentence, and `ToolResults.RunAsync` can only return text.
- Channel events go out through `ChannelEventSink`, which takes a `LiveUpdate` built from a `TawkEvent` that tawk sent. Nothing tawk-mcp produces by itself reaches the channel yet.

## 4. Step 0: the upstream change in tawk

Fields may be added to results and notifications without a new protocol version, so all of this stays at version 1.

1. `download_media` answers `{"path":"..."}` when the file is already there, and `{}` when the download has started.
2. A new notification when a download finishes: `{"evt":"media_ready","chat":{...},"message_id":"...","path":"...","type":"audio"}`, sent under the same visibility rules as `message`.
3. Optional, later: `duration_s` on audio messages, so tawk-mcp can refuse a two-hour recording before fetching it.

tawk already checks that a path lies inside its media folder before storing it. tawk-mcp repeats the check on its side (regular file, size limit) and never returns the path to the model.

### Ruled out: reading tawk.db directly

The earlier draft offered this as a way to avoid changing tawk. It does not work:

- `tawk.db` may be encrypted with SQLCipher, and tawk-mcp has no passphrase.
- `messages` is keyed by `(account_id, id)`, so a message id alone does not name a row.
- Media lives in tawk's cache folder (`media.media_dir`, user-settable), and the database in its data folder. Neither is the runtime folder tawk-mcp knows.
- It would pass by tawk's rules for locked, hidden and excluded chats, against "tawk is the authority" in `INTENT.md`.

## 5. Image viewing

- **Tool:** `view_image(messageId, account?)`. Read-only. Works for `image` and `sticker`.
- **Flow:** ask tawk for the path (`download_media`). If the file is there, read it, work out the MIME type from its first bytes, and return an image block. If tawk is still fetching it, wait for `media_ready` up to the request timeout, then answer "still downloading, call again" so the call never hangs.
- **Limits:** a size limit (default 5 MB) with a clear error above it. No resizing in the first version.
- **Plumbing:** `ToolResults` gains a way to return content blocks, and the manager returns a small result type (bytes, MIME type) in place of a string.
- **Safety:** a picture can carry text meant for the model, and it cannot be fenced. The tool description and the server instructions say that what a picture shows is other people's content, the same as message text. The caption still goes through `UntrustedTextFence`.
- **Not planned:** `analyze_image` with a local vision model. `INTENT.md` puts choosing and calling a model out of scope, and the client's model already sees the picture. It can be added later behind the same engine choice as transcription if someone needs pictures kept away from the client's model service.

## 6. Voice transcription

### 6.1 Fire and forget

`transcribe_message` never waits for the text.

1. The tool checks the request (transcription is on, the message is `audio`, the options are allowed), puts a job in a queue and answers at once: `Transcribing message <id> as job <jobId>. A channel event follows when it is done.`
2. A hosted worker takes the job: gets the file from tawk once (waiting for `media_ready` if needed), then hands it to the chosen engine once for each language asked for, and collects each text. Section 6.4 describes these passes.
3. When every pass has ended, the worker publishes the outcome as one channel event and on the event stream.

A second request for the same message with the same options, including the same set of languages, joins the job already running and returns its id.

### 6.2 The signal

One channel event per job, sent to the sessions `ChannelEventSink` would pick (`--channel auto|on|off`):

```
method:  notifications/claude/channel
content: a header line, then the transcript fenced as untrusted text
content: a header line, then one fenced block per language, each under
         its own label line (for example "af:" and "en:")
meta:    type=transcript  status=done|partial|failed
         job_id  message_id  chat_jid  chat_name  sender
         account  account_id
         languages (comma separated, as asked for; a detected one is
         written as auto:xx)  failed_languages
         engine  model  task  duration_s
```

- The job sends one event, when all its passes have ended, however many languages were asked for. The agent gets every transcription together and can compare them.
- Each transcript is what another person said, so each is fenced exactly like message text. The label lines and the header are written by tawk-mcp, outside the fences, so nothing spoken can pose as one.
- `status` is `done` when every pass produced text, `partial` when some did, and `failed` when none did. A failed pass has a short reason (`no transcriber`, `too long`, `download failed`, `engine error`, `timed out`), never a stack trace.
- The server-sent event stream gets a `transcript` event with the same fields, for programs that are not Claude Code.
- `get_transcript(jobId)` is the fallback for clients without channels and for a session that reconnected. It answers `queued`, `running` (with the passes finished so far), or the final state with every transcript, laid out as in the event.
- A transcript event counts as a round for the workflow cadence, the same as a message.

A job is not a `TawkEvent` that tawk sent, so the sink needs a second way in. Planned shape: a `TranscriptEvent` in `Core`, a `LiveUpdate` built from it by `TranscriptionManager` and handed to `IEventSink`, and one more arm in `ChannelEventSink.Meta`. The sink stays the only place that writes to the channel. Section 7 gives the full design.

### 6.3 Choosing how to transcribe

Two levels. The user sets what exists and the defaults, when starting tawk-mcp. The agent picks among those per call.

Set by the user (flags, with matching `TAWKMCP_*` variables):

| Flag | Meaning | Default |
| --- | --- | --- |
| `--transcribe` | `off`, `auto`, `http`, `command` or `embedded`. `auto` uses a running transcriber when one answers and the in-process model when none does (section 6.7) | `off` |
| `--transcribe-url` | The endpoint for `http`, speaking the OpenAI transcription API (`/v1/audio/transcriptions`), which whisper.cpp's server, faster-whisper-server and others offer | none |
| `--transcribe-command` | The program for `command`, with `{file}`, `{language}`, `{model}` and `{task}` filled in; the text is read from its output | none |
| `--transcribe-model` | The default model name | the engine's own |
| `--transcribe-model-dir` | The folder of model files for the in-process engine | `~/.local/share/tawk-mcp/models` |
| `--transcribe-idle-unload-m` | Minutes without a job before the in-process model is unloaded. 0 keeps it loaded | 15 |
| `--transcribe-models` | The model names an agent may ask for | only the default |
| `--transcribe-language` | The default languages: one or more ISO 639-1 codes or `auto`, comma separated (for example `af,en`) | `auto` |
| `--transcribe-max-languages` | The most languages one call may ask for | 3 |
| `--transcribe-max-seconds` | The longest recording accepted | 600 |
| `--transcribe-timeout-s` | How long one job may run | 300 |
| `--transcribe-concurrency` | Jobs running at once | 1 |

Chosen per call, all optional:

| Argument | Meaning |
| --- | --- |
| `languages` | A list of ISO 639-1 codes, and `auto` if wanted. One transcription is made for each. Overrides the default |
| `task` | `transcribe` (in the spoken language) or `translate` (into English) |
| `model` | One of the allowed model names |
| `prompt` | A short hint for names and jargon, passed to the engine |

Rules:

- An agent can never supply a URL or a command. Both come only from the user's flags, in line with tawk keeping "settings that run programs" out of reach.
- The `http` endpoint must be on loopback unless the user adds `--transcribe-remote on`. With that off, a voice note never leaves the machine.
- With `--transcribe off` the tools are not registered at all, the way `--memory off` removes the memory tools.
- When no `languages` are given and memory holds stated languages for the contact, those are used before the configured default.
- `task`, `model` and `prompt` apply to every pass of the call. A different model for each language is two calls.

Behind this sits one `ITranscriber` interface in `ResourceAccess` with an implementation per engine, bound in the composition root. Section 6.7 covers the in-process one and how tawk-mcp moves between a running transcriber and its own.

### 6.4 Several languages in one call

Whisper-style engines handle one language for each run. Told a voice note is Afrikaans, they write the English parts badly or render them in Afrikaans, and `auto` picks whichever language the first seconds are in. For a note that switches language, the useful answer is several transcriptions of the same audio, one for each language, which the agent reads side by side.

- A job has one **pass** for each language asked for. Duplicates are dropped and the order asked for is kept. `auto` counts as a language and may be listed with others, for example `["auto","af","en"]`.
- The file is fetched once. Passes run one after another inside the job, so a job with three languages takes about three times as long and holds one concurrency slot throughout.
- `--transcribe-timeout-s` bounds each pass, and the job's bound is that times its number of passes. `--transcribe-max-languages` keeps a call from asking for twenty.
- A pass that fails does not stop the others. The outcome is `partial`, with the failed languages named.
- With `task=translate`, each pass translates into English with that language as the source hint.
- `ITranscriber` stays as it is: one file, one language, one transcript. The loop over languages lives in `TranscriptionManager`, so no engine has to know about passes.

Not planned: cutting the audio into segments and detecting the language of each. It needs voice activity detection and per-segment language scoring, which is a feature of its own. The decoder that the in-process engine brings (section 6.5) makes it possible later.

### 6.5 Audio format

WhatsApp voice notes are Opus in an Ogg container.

- The `http` engines named above accept that directly, so tawk-mcp sends the file as it is.
- The `command` engine gets the file path, and converting it (for example with `ffmpeg`) is the command's business.
- The in-process engine needs 16 kHz mono PCM. tawk-mcp decodes and resamples in managed code (`Concentus` with its Ogg reader), so no `ffmpeg` and no extra native library is needed for audio. Other containers (an `.m4a` forwarded as audio) are refused by this engine with `unsupported format`.

### 6.6 What is kept

`INTENT.md` says tawk-mcp stores no messages. Transcripts follow that: finished jobs are held in memory only, a bounded number (default 50) for a bounded time (default 30 minutes), so `get_transcript` works, and they are gone on restart. Nothing is written to disk or to the log.

### 6.7 The in-process model: one instance, reused

**When it is used.** With `--transcribe embedded`, always. With `--transcribe auto`, whenever no running transcriber answers: no `--transcribe-url` was given, the endpoint refuses the connection, or it keeps failing. tawk-mcp then loads a Whisper model through `Whisper.net` (whisper.cpp) inside its own process. Nothing is spawned and no port is opened.

**One instance, never two.** This is a hard rule, enforced at three levels:

- **In the process.** A single model host owns the loaded model. It loads on first use, and every job and every language pass reuses it. Inference goes through the host one call at a time, so with this engine `--transcribe-concurrency` is treated as 1. Asking for a different model unloads the current one before the next is loaded. Two are never in memory together.
- **Across tawk-mcp processes.** stdio mode can mean several tawk-mcp processes on one machine. A lock file next to the model folder, in the style of `FileSyncLock`, is held for as long as a model is loaded. A process that finds it held waits a bounded time, then fails the pass with `transcriber busy`. It does not load a second copy.
- **Against a running transcriber.** tawk-mcp never loads its own model while the external one is answering, and unloads its own before sending work back to the external one.

**Reuse.** The model stays loaded between jobs, so the second voice note does not pay the load time again. After `--transcribe-idle-unload-m` minutes without a job it is unloaded and the lock released, and the next job loads it again.

**Resilience.**

- *Choosing a route.* A circuit breaker (the existing `ICircuitBreaker`) sits on the external transcriber. While it is closed, jobs go there. A refused connection or repeated failures open it, the in-process model takes over, and the pass that was in flight is retried there, so the job still ends `done`. After the cooldown one trial goes to the external transcriber, and on success work moves back and the in-process model is unloaded.
- *The in-process model failing.* A failed load or a failed inference disposes the instance, waits by `IBackoffPolicy`, and loads again, once for each pass. A second failure fails that pass with `engine error` and leaves the host empty, so the next job starts clean. A pass that hits its timeout is cancelled and the instance is disposed, since a cancelled native call cannot be trusted afterwards.
- *Never hang.* Loading, waiting for the lock and each inference all have bounds. The worker always moves on.
- *Shutdown.* The host unloads the model and releases the lock when tawk-mcp stops. A lock left by a killed process is detected as stale and taken over.
- *What it cannot survive.* A crash inside the native library takes the tawk-mcp process with it. systemd or Docker restarts it, the stale lock is cleared, and jobs in the queue are lost (section 6.6). An out-of-process transcriber is the safer choice for anyone who sees this.

**Where the model comes from.** tawk-mcp has no update checks and sends nothing anywhere by itself, so it never downloads a model silently. The model is a GGML file in `--transcribe-model-dir`, put there by the user or by `tawk-mcp fetch-model <name>`, a command the user runs on purpose. With no model file, an `auto` or `embedded` pass fails with `no model installed` and the one-line command to fix it. The names in the folder are the models an agent may ask for.

**Cost.** `Whisper.net` and its CPU runtime add native libraries to the publish output and the image. A `base` model is about 150 MB on disk and `small` about 500 MB, with memory use somewhat above that. CPU is the default. GPU runtimes are a later option.

## 7. Design

The feature follows the rules in `AGENT.md` without exception. This section says how each one applies, so a review can check the code against it.

### 7.1 iDesign layers

Host composes. Clients call managers. Managers call engines and resource access. Everything may use core. Managers never call each other.

| Layer | Type | Its one concern |
| --- | --- | --- |
| Core | `MediaReadyEvent` | tawk's notice that a file has arrived |
| Core | `MediaFile` | A file tawk named: path, type, size |
| Core | `ImageData` | Bytes and MIME type handed to a tool |
| Core | `TranscriptionOptions` | What the user configured: engine, defaults, limits |
| Core | `TranscriptionRequest` | One call's choices: message, languages, task, model, prompt |
| Core | `TranscriptionJob` | A job's id, request, state and passes |
| Core | `TranscriptionPass` | One language of a job: its state, and its transcript or its reason for failing |
| Core | `TranscriptionState` | `Queued`, `Running`, `Done`, `Partial`, `Failed` |
| Core | `TranscriptionTask` | `Transcribe`, `Translate` |
| Core | `Transcript` | One pass's text with its language (asked for or detected), engine, model and duration |
| Core | `TranscriptEvent` | A finished or failed job, as a live update |
| Core | `TranscriptionException` | A refusal or failure with a reason safe to show |
| ResourceAccess | `IMediaFiles`, `DiskMediaFiles` | Checking and reading a file tawk named |
| ResourceAccess | `ITranscriber` | Turning one audio file into text |
| ResourceAccess | `HttpTranscriber` | `ITranscriber` over the OpenAI transcription API |
| ResourceAccess | `CommandTranscriber` | `ITranscriber` by running the user's command |
| ResourceAccess | `EmbeddedTranscriber` | `ITranscriber` by decoding the file and asking the model host |
| ResourceAccess | `FailoverTranscriber` | `ITranscriber` made of two others and a circuit breaker: the route between a running transcriber and the in-process one |
| ResourceAccess | `IWhisperModelHost`, `WhisperModelHost` | Owning the one loaded model: load, reuse, swap, idle unload, dispose |
| ResourceAccess | `IModelLock`, `FileModelLock` | One loaded model for each machine, across processes |
| ResourceAccess | `IModelFiles`, `DiskModelFiles` | Listing and locating model files; fetching one when the user asks |
| ResourceAccess | `IAudioDecoder`, `OggOpusDecoder` | Ogg Opus to 16 kHz mono PCM |
| ResourceAccess | `ITranscriptionJobStore`, `InMemoryTranscriptionJobStore` | Holding the queue and recent results, with their bounds |
| Engines | `IMediaTypeSniffer`, `MediaTypeSniffer` | The MIME type from a file's first bytes |
| Engines | `ITranscriptionPolicy`, `TranscriptionPolicy` | Whether a request is allowed, and its options after defaults are applied |
| Engines | `ITranscriptionNoticeFormatter`, `TranscriptionNoticeFormatter` | The header line and fenced body of the channel event (`ITranscriptFormatter` already names the chat transcript formatter) |
| Managers | `IMediaViewingManager`, `MediaViewingManager` | The use case "show me this picture" |
| Managers | `ITranscriptionManager`, `TranscriptionManager` | The use cases "start a job", "run the next job", "read a job" |
| Clients | `MediaTools` | The `view_image` tool |
| Clients | `TranscriptionTools` | The `transcribe_message` and `get_transcript` tools |
| Clients | `TranscriptionWorker` | A hosted service that asks the manager to run the next job |
| Clients | `ChannelEventSink`, the event stream sink | One more arm each, for `TranscriptEvent` |
| Host | `TawkMcpOptions`, `TawkMcpOptionsBinder`, `HelpText`, `TawkMcpComposition` | Flags and bindings |

How the calls run:

- `TranscriptionTools` calls `ITranscriptionManager.StartAsync` and returns the job id.
- `TranscriptionWorker` loops on `ITranscriptionManager.RunNextAsync`. It holds no logic of its own, the way `MemorySyncService` only drives `IMemorySyncManager`.
- `TranscriptionManager` orchestrates and decides nothing itself: `ITranscriptionPolicy` judges the request, `ITawkControl` asks tawk for the file, `IMediaFiles` checks it, `ITranscriber` produces the text for each language in turn, `ITranscriptionJobStore` keeps the job, `ITranscriptionNoticeFormatter` writes the notice, and `IEventSink` carries it up.
- The transcription manager needs a media file and so does the viewing manager. Neither calls the other. Both use `ITawkControl` and `IMediaFiles` from resource access.
- The one upward call, manager to channel, goes through `IEventSink`, an interface the manager layer already owns.

### 7.2 SOLID

- **Single responsibility.** Each type in the table has one reason to change. The queue's bounds change in the job store, the allowed languages in the policy, the wire format in a transcriber, the wording in the formatter.
- **Open for extension, closed for change.** The in-process engine and the failover are new `ITranscriber` types and bindings. `TranscriptionManager`, the tools and the policy do not change for them, and a further engine would be the same.
- **Liskov substitution.** Every `ITranscriber` honours the same contract: it takes a file, one language and the resolved options, returns one `Transcript` or throws `TranscriptionException`, and respects the cancellation token and the timeout. The manager cannot tell which one it holds, and the tests run on a fake.
- **Interface segregation.** New behaviour gets new, small interfaces. `INotificationFormatter`, `ITawkControl` and `IMessageManagementManager` are left as they are: the path arrives in the existing `download_media` answer and on the existing event stream.
- **Dependency inversion.** Managers and clients depend only on the interfaces above. Concrete types are named only in `TawkMcpComposition`.

### 7.3 One type per file

Every class, record, enum and interface above is its own file named after it, with no nested types. That includes the two enums and the exception. Fakes for the tests (`FakeTranscriber`, `FakeMediaFiles`, `FakeTranscriptionJobStore`) go in `tests/Tawk.Mcp.Tests/Fakes`, one per file, hand-written.

### 7.4 Composition over inheritance

- There is no transcriber base class. Each transcriber implements `ITranscriber` and is given what it needs: an `HttpClient`, a process runner in the style of `IGitRunner`, or the model host and the decoder.
- Failover is composition: `FailoverTranscriber` holds a primary `ITranscriber`, a fallback `ITranscriber` and an `ICircuitBreaker`, and is itself an `ITranscriber`. The manager still sees one.
- The model host is bound as a singleton and is the only type that touches `Whisper.net`'s factory. `EmbeddedTranscriber` never loads a model itself.
- The engine, or the pair behind the failover, is chosen once, in `TawkMcpComposition`, from `--transcribe`. Nothing below the composition root switches on the engine's name.
- With `--transcribe off`, the tools, the worker and the transcriber are simply not bound, as with `--memory off`.
- All new classes are `sealed`.
- The one place a new type extends another is data: `MediaReadyEvent` and `TranscriptEvent` are sealed records under `TawkEvent`, like `MessageEvent` and `ReadEvent`, because `LiveUpdate` carries a `TawkEvent` and the sinks match on its kind. They hold no behaviour. `TawkEvent`'s summary is reworded to cover an event tawk-mcp raises itself.

### 7.5 Separation of concerns

- **I/O against rules.** Engines stay free of I/O. The sniffer is given bytes, the policy is given options and a request. Reading files, calling HTTP and running a process live only in resource access.
- **Transport against use case.** Tools shape arguments and results. Channel and stream sinks shape events. Neither knows how a transcript is made.
- **Lifetime against use.** Loading, sharing and unloading the model is the host's concern. Turning audio into text with whatever the host holds is the transcriber's. Choosing between engines is the failover's.
- **State against flow.** The job store owns the queue, the join of duplicate requests and the expiry of old results. The manager owns the order of steps.
- **Trust.** Fencing happens in one place, the notice formatter through `IUntrustedTextFence`, so no caller can forget it.
- **Configuration.** Flags are parsed in the host into `TranscriptionOptions`, a core record. Lower layers never read the environment.

### 7.6 Other standing rules that apply

- **Never hang.** Waiting for `media_ready`, the HTTP call and the command each have a bound, and the worker moves on when one is hit.
- **Strict build.** Nullable on, analysers at `latest-recommended`, warnings as errors, nothing suppressed in code.
- **`ToolResults`** gains one method that returns content blocks. Its existing text methods are untouched.

## 8. Docker

The container mounts only tawk's runtime folder and `/data`. To read a file tawk names, it needs tawk's media folder mounted read-only at the same path tawk reports. `scripts/docker-run.sh`, `compose.yaml` and `install.sh` gain that mount, added only when image viewing or transcription is switched on. A transcriber on the host is reached through the host's loopback, which needs the usual host gateway setting. Without the mount, the tools answer with a plain message saying what to mount.

The in-process engine needs no reach to the host's loopback. Its model folder sits in the `/data` volume, so a model survives a new container. The image grows by the `Whisper.net` runtime and ships with no model in it.

## 9. Documents to update

- `INTENT.md`: scope gains viewing pictures and transcribing voice notes with a transcriber the user runs; "Choosing or calling a model" is narrowed to say the one exception; the folders and files line notes the media folder is read, never written.
- `SECURITY.md`: pictures and transcripts as untrusted content, the loopback rule, the extra mount, and what `--transcribe-remote on` gives up.
- `MANUAL.md`, `CONFIGURATION.md`, `README.md`, `AGENT_SETUP.md`: the tools, the flags, the channel event.
- `TawkServerInstructions`: transcription answers later through the channel; do not call again while a job runs.

## 10. Order of work

1. tawk: path in the `download_media` answer and the `media_ready` notification, with CONTROL.md.
2. tawk-mcp: `media_ready` decoding, `IMediaFiles`, content blocks in `ToolResults`, `view_image`.
3. tawk-mcp: the job queue, worker, `HttpTranscriber`, `transcribe_message`, the channel and stream events, `get_transcript`.
4. tawk-mcp: `CommandTranscriber`, the per-call options and their checks, several languages, the contact language default.
5. tawk-mcp: the in-process engine: decoder, model host, lock, `EmbeddedTranscriber`, `fetch-model`.
6. tawk-mcp: `FailoverTranscriber` and `--transcribe auto`.
7. Docker mount, documents.

Each step ships with tests in the existing style: the fake tawk server for the control side, a fake `ITranscriber` for jobs, and `ChannelDeliveryTests` for the event.

## 11. Open questions

- **Access level.** tawk's source calls `download_media` a read, yet tawk-mcp lists it among writes and its tool says it needs `manage`. Viewing and transcribing should be possible at `read`. To settle with step 0.
- **Image blocks in SDK 2.2.0.** The image content block type is assumed from the MCP specification and was not checked against the pinned `ModelContextProtocol` 2.2.0 package.
- **Audio blocks.** MCP also defines an audio content block. Returning the voice note itself to a client whose model hears audio would need no transcriber. Worth a `listen_message` tool only once a client we use supports it.
- **Whether "in process" may ever mean a child process.** This plan loads the model inside tawk-mcp, as asked. A supervised child (a whisper server tawk-mcp starts and restarts) would survive native crashes better, at the cost of a second program to ship. Kept out unless crashes prove to be a problem.
- **Packages.** `Whisper.net`, its runtime and `Concentus` are new dependencies, to be checked for licence and for a clean build under warnings as errors on .NET 10.
- **Automatic transcription.** Transcribing every incoming voice note without being asked is left out. It can be a later flag built on the same queue.

## 12. Decisions made while building

These replace what the sections above say where they differ.

- **The user's choices live in tawk's settings panel.** Settings, Automation, Voice note transcription holds the model (a list to choose from: `tiny`, `base`, `small`, `medium`, `large-v3-turbo`, `large-v3`), the default languages, and a switch for transcribing every voice note as it arrives. They are in `[automation]`, so an agent can read them and cannot change them.
- **tawk's settings override tawk-mcp's.** For the model, the languages and the automatic switch, tawk's panel comes first. `--transcribe-model`, `--transcribe-language` and `--transcribe-auto` stand in only for a tawk that does not have these settings or is not running. The built-in values are last: `tiny`, `auto`, off.
- **The smallest model is the default.** `tiny`, until the user picks another.
- **Automatic transcription is built**, as a switch, where section 11 had left it out. `AutoTranscriptionSink` offers each incoming voice note, and the switch is read as each one arrives, so a change in the panel takes hold within seconds.
- **Two managers, not one.** `TranscriptionManager` takes requests and answers about jobs. `TranscriptionRunManager` runs them and announces the outcome. The automatic sink is an event sink that needs the first, and the second needs the event sinks, so one manager would have depended on itself.
- **`ITranscriptionPreferences`** in resource access reads the choices with `get_settings` and keeps them for ten seconds.
- **`download_media` also names the type and the chat** when the file is there: `{"path","type","chat":{"jid","name"}}`. The `media_ready` event carries the same.
- **What stays in tawk-mcp's flags** is what belongs to the installation: the engine, the transcriber's address or command, whether it may be on another machine, the models an agent may ask for beyond the chosen one, and the limits.

## 13. Where the work stands

| Step | State |
| --- | --- |
| tawk: path in the `download_media` answer, `media_ready`, the settings and their submenu | Done on a branch, tests pass |
| tawk-mcp: `view_image` | Done, tests pass |
| tawk-mcp: job queue, worker, `http` and `command` engines, `transcribe_message`, `get_transcript`, channel and stream events | Done, tests pass |
| tawk-mcp: several languages, per-call options, automatic transcription, settings read from tawk | Done, tests pass |
| tawk-mcp: the in-process engine and failover (section 6.7) | Not started |
| tawk-mcp: the contact's language as a default | Not started |
| Docker mount, and the manual, configuration, intent and security documents | Not started |
