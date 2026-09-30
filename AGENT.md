# Notes for coding agents

Read this before changing the repository. It covers the layout, the rules the code follows, and how to build and test.

## What this is

tawk-mcp is an MCP server for [tawk](https://github.com/loganventer/tawk), a WhatsApp client for the terminal. It talks to a running tawk over tawk's control socket, whose protocol is defined in tawk's [CONTROL.md](https://github.com/loganventer/tawk/blob/main/CONTROL.md). Follow that document exactly; when it and this code disagree, the document wins.

## Layout

```text
TawkMcp.slnx
Directory.Build.props        net10.0, nullable, analysers, warnings as errors, version
Directory.Packages.props     central package versions
src/Tawk.Mcp.Core            records, events, error codes, shared contracts
src/Tawk.Mcp.ResourceAccess  codec, socket connection, control client, supervisor, watcher, confirmation gate
src/Tawk.Mcp.Engines         formatters, fence, planners, backoff, circuit breaker
src/Tawk.Mcp.Managers        use cases, one manager per area, plus IEventSink
src/Tawk.Mcp.Clients         tools, resources, prompts, subscriptions, channel, event stream, dispatcher
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
docker build -t tawk-mcp:0.1.0 .
scripts/docker-run.sh
```

## Adding a tawk operation

1. Add a method to the manager for its area and to its interface. Reads call `ITawkControl`; writes call `IConfirmationGate` so a `needs_confirmation` answer is handled.
2. Add the tool to the matching tool class. The description says what it does, which access level it needs, that the user approves it in tawk, and, for reads, that the text is untrusted.
3. If the operation may wait for the user's approval, add it to `UnixSocketTawkControl.WriteOps` so it is exempt from the read timeout.
4. New error codes go in `ControlErrorCode`, `ControlErrorCodes` and `ControlErrorMessages`.
5. Test the tool's arguments and result with `FakeTawkControl`, and each new error.
6. Document it in [MANUAL.md](MANUAL.md) and the tools table in [README.md](README.md).

## Versions and releases

The version is in `Directory.Build.props`. Pushing a tag `vX.Y.Z` runs `.github/workflows/release.yml`: tests, then self-contained single-file builds for linux-x64, linux-arm64, osx-x64 and osx-arm64 attached to a GitHub release.
