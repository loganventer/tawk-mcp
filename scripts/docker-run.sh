#!/usr/bin/env bash
# Builds the tawk-mcp image and runs it as your user, next to your running tawk.
# The container serves streamable HTTP, tawk-mcp's default transport, on loopback only.
set -euo pipefail

cd "$(dirname "$0")/.."
image="tawk-mcp:0.7.0"
name="tawk-mcp"
port="${TAWKMCP_PORT:-8765}"
runtime="${XDG_RUNTIME_DIR:-$HOME/.local/state}"
tawk_dir="$runtime/tawk"
data_dir="${XDG_CONFIG_HOME:-$HOME/.config}/tawk-mcp"

# Containers run in UTC unless told otherwise, which would put every time tawk-mcp shows or reads
# (message times, "since" cutoffs, follow-up dates) off by your offset. Give it this machine's zone.
host_timezone() {
  if [ -n "${TZ:-}" ]; then printf '%s\n' "$TZ"; return; fi
  if [ -L /etc/localtime ]; then
    zone="$(readlink /etc/localtime)"
    case "$zone" in */zoneinfo/*) printf '%s\n' "${zone##*/zoneinfo/}"; return ;; esac
  fi
  if [ -r /etc/timezone ]; then cat /etc/timezone; return; fi
  printf 'UTC\n'
}
timezone="$(host_timezone)"

# Docker would create missing bind folders owned by root, which tawk and tawk-mcp could not use.
mkdir -p "$tawk_dir" "$data_dir"
chmod 0700 "$tawk_dir" "$data_dir"

docker build -t "$image" .
docker rm -f "$name" >/dev/null 2>&1 || true
docker run -d --name "$name" --restart unless-stopped \
  --user "$(id -u):$(id -g)" \
  -p "127.0.0.1:$port:8765" \
  -v "$tawk_dir:/run/tawk" \
  -v "$data_dir:/data" \
  -e TAWK_CONTROL_SOCKET=/run/tawk/control.sock \
  -e TAWKMCP_TOKEN_FILE=/data/token \
  -e TZ="$timezone" \
  "$image" >/dev/null

for _ in $(seq 1 50); do
  [ -s "$data_dir/token" ] && break
  sleep 0.2
done
token="$(cat "$data_dir/token")"

cat <<INFO
tawk-mcp is running at http://127.0.0.1:$port/mcp (times in $timezone)
Health: $(curl -fsS "http://127.0.0.1:$port/healthz" 2>/dev/null || echo "not answering yet")

Bearer token (kept in $data_dir/token):
  $token

Claude Code:
  claude mcp add --transport http tawk http://127.0.0.1:$port/mcp --header "Authorization: Bearer $token"

VS Code (.vscode/mcp.json):
  {
    "servers": {
      "tawk": {
        "type": "http",
        "url": "http://127.0.0.1:$port/mcp",
        "headers": { "Authorization": "Bearer $token" }
      }
    }
  }

Claude Desktop (claude_desktop_config.json, through the mcp-remote bridge):
  {
    "mcpServers": {
      "tawk": {
        "command": "npx",
        "args": ["-y", "mcp-remote", "http://127.0.0.1:$port/mcp", "--header", "Authorization: Bearer $token"]
      }
    }
  }

New messages as server-sent events:
  curl -N -H "Authorization: Bearer $token" http://127.0.0.1:$port/events
INFO
