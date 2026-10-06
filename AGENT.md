# Notes for coding agents

Read this before changing the repository. It covers the layout, the rules the code follows, and how to build and test.

## What this is

tawk-mcp is an MCP server for [tawk](https://github.com/loganventer/tawk), a WhatsApp client for the terminal. It talks to a running tawk over tawk's control socket, whose protocol is defined in tawk's [CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md). Follow that document exactly; when it and this code disagree, the document wins.

## Layout

```text
TawkMcp.slnx
Directory.Build.props        net10.0, nullable, analysers, warnings as errors, version
Directory.Packages.props     central package versions
src/Tawk.Mcp.Core            records, events, error codes, shared contracts; Okf/ concepts and links; Sync/ rows, plans, options
src/Tawk.Mcp.ResourceAccess  codec, socket connection, control client, supervisor, watcher, confirmation gate; Memory/ SQLite stores; Sync/ snapshots, the git remote over SSH
src/Tawk.Mcp.Engines         formatters, fence, planners, backoff, circuit breaker, schedule jitter; Memory/ voice rules, field catalog; Knowledge/ OKF bundles; Sync/ merge rules
src/Tawk.Mcp.Managers        use cases, one manager per area, plus IEventSink; Memory/ voices, contacts, templates; Knowledge/; Sync/
src/Tawk.Mcp.Clients         tools, resources, prompts, subscriptions, channel, event stream, dispatcher; Workflow/ the workflow check
src/Tawk.Mcp.Host            composition root, Program, middleware, endpoints (assembly tawk-mcp)
tests/Tawk.Mcp.Tests         NUnit tests, Fakes/ holds every fake
```

[ARCHITECTURE.md](ARCHITECTURE.md) describes each component; [HOW_IT_WORKS.md](HOW_IT_WORKS.md) shows the flows.

## Rules

- **iDesign layers.** Host composes; clients call managers; managers call engines and resource access; everything may use core. Managers never call each other; a tool that needs two managers takes both. Upward calls go only through interfaces defined in a lower layer (`IEventSink`, `IUserConfirmation`, `ICircuitBreaker`, `IBackoffPolicy`).
- **One type per file.** Every class, record, enum and interface in its own file, named after it. No nested types.
- **Dependency inversion.** Depend on interfaces. Bind them only in `TawkMcpComposition`. Do not new up services anywhere else.
- **Composition over inheritance**, separation of concerns, file-scoped namespaces.
- **The build is strict.** Nullable enabled, `latest-recommended` analysers, warnings as errors. Do not suppress a warning in code to get past it; fix it, or, if the rule does not fit this codebase, turn it off in `.editorconfig` with a comment saying why.
- **Untrusted text.** Anything written by other people must reach a model through `IUntrustedTextFence`. Never put it in a tool description or a header unfenced, except names flattened as `NotificationFormatter` does.
- **Confirmation tokens** never leave `ConfirmationGate`. Do not log them, return them, or add any tool that confirms.
- **Never hang.** Every wait needs a bound, except a write waiting for the user's approval in tawk.
- **Repositories own the SQL.** Every table is reached through a store interface in `ResourceAccess`; nothing above that layer writes SQL. SQLite has no stored procedures, so a write of several statements runs in one transaction inside its store.
- **Every delete leaves a tombstone** and every write clears it (`Tombstones`), or memory sync brings the row back from another machine.
- **Sync has no default destination, ever.** The repository and the SSH key are always the user's own, set per instance. Do not add a default repository, key or owner anywhere in the code, the image or the documentation.
- **Only fixed text and the user's own instructions file become instructions.** Never load text from a chat or from the memory database into the server instructions or the workflow check.
- **Tests use hand-written fakes only**, no mocking library. Put fakes in `tests/Tawk.Mcp.Tests/Fakes`, one per file. Socket behaviour is tested against `FakeTawkServer` in a temp folder.

## Writing

- Plain British English in code comments, documentation, tool descriptions and commit messages.
- No em dashes, and no dashes used as separators or asides. Use commas, colons, full stops or brackets.
- Avoid "not X but Y" constructions; say the thing directly.
- Comments only where the reason is not obvious from the code.
- Commit messages in the project's own voice, with no mention of AI assistants or code generation, and no co-author or "generated with" lines.

## Build and test

```sh
dotnet build TawkMcp.slnx -warnaserror
dotnet test TawkMcp.slnx
```

The tests need a Unix-like system (they use Unix domain sockets and file modes). They run in a few seconds.

Try it by hand:

```sh
dotnet run --project src/Tawk.Mcp.Host -- --help
TAWK_CONTROL_SOCKET=/tmp/none.sock dotnet run --project src/Tawk.Mcp.Host -- --port 18765
curl http://127.0.0.1:18765/healthz
```

The installer (`install.sh`) mirrors tawk's own: the same `say`, `warn`, `die` and `have` helpers, and the same shape of options. It works from a checkout or piped from GitHub. Check changes with `bash -n install.sh` and shellcheck (`docker run --rm -v "$PWD:/mnt" -w /mnt koalaman/shellcheck:stable install.sh scripts/docker-run.sh` if it is not installed), then try it for real into a temporary prefix and remove what it installed. By default the installer registers a service and may turn on linger; use `--no-service` for a quick trial, and when you test registering, undo linger afterwards if it was off before:

```sh
tmp="$(mktemp -d)"
TAWKMCP_TOKEN_FILE="$tmp/token" ./install.sh --prefix "$tmp" --no-service
"$tmp/bin/tawk-mcp" --version
TAWKMCP_TOKEN_FILE="$tmp/token" ./install.sh --prefix "$tmp" --uninstall && rm -rf "$tmp"
```

Docker:

```sh
docker build -t tawk-mcp:0.6.1 .
scripts/docker-run.sh
```

## Adding a tawk operation

1. Add a method to the manager for its area and to its interface. Reads call `ITawkControl`; writes call `IConfirmationGate` so a `needs_confirmation` answer is handled.
2. Add the tool to the matching tool class. The description says what it does, which access level it needs, that the user approves it in tawk, and, for reads, that the text is untrusted.
3. If it reaches WhatsApp, give the tool an optional `account` (`[Description(ToolText.Account)] string? account = null`, before `progress` and `cancellationToken`) and run it with `ToolResults.RunAsync(accounts, account, ...)`. The manager does not take the account: the scope and `AccountScopedTawkControl` carry it.
4. If the operation may wait for the user's approval, add it to `UnixSocketTawkControl.WriteOps` so it is exempt from the read timeout.
5. New error codes go in `ControlErrorCode`, `ControlErrorCodes` and `ControlErrorMessages`.
6. Test the tool's arguments and result with `FakeTawkControl`, and each new error.
7. Document it in [MANUAL.md](MANUAL.md) and the tools table in [README.md](README.md).

## Versions and releases

The version is in `Directory.Build.props`. There is no CI: run the tests locally before pushing, and `install.sh` builds from source.
