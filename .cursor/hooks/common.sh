#!/bin/bash
# common.sh - Shared utilities for claude-mem cursor hooks
# Source this file in other hook scripts: source "$(dirname "$0")/common.sh"

# Check required dependencies
check_dependencies() {
  local missing=0
  for cmd in jq curl; do
    if command -v "$cmd" &>/dev/null || command -v "${cmd}.exe" &>/dev/null; then
      continue
    fi
    echo "Warning: $cmd not found - claude-mem hooks will not function" >&2
    missing=1
  done
  return $missing
}

# Prefer Windows curl.exe when Git Bash shims are unavailable.
run_curl() {
  if command -v curl &>/dev/null; then
    curl "$@"
  else
    curl.exe "$@"
  fi
}

# Read JSON from stdin safely
read_json_input() {
  local input
  input=$(cat 2>/dev/null)
  if [ -z "$input" ]; then
    echo "{}"
    return
  fi
  if ! echo "$input" | jq . &>/dev/null; then
    echo "{}"
    return
  fi
  echo "$input"
}

# Resolve home directory (Git Bash sets HOME; Windows hooks may only have USERPROFILE).
get_user_home() {
  if [ -n "$HOME" ]; then
    echo "${HOME//\\//}"
  elif [ -n "$USERPROFILE" ]; then
    echo "${USERPROFILE//\\//}"
  else
    echo "/tmp"
  fi
}

# Get worker port from settings
get_worker_port() {
  local settings_file="$(get_user_home)/.claude-mem/settings.json"
  local port=37777
  if [ -f "$settings_file" ]; then
    local configured_port
    configured_port=$(jq -r '.CLAUDE_MEM_WORKER_PORT // empty' "$settings_file" 2>/dev/null)
    if [ -n "$configured_port" ] && [ "$configured_port" -ge 1 ] && [ "$configured_port" -le 65535 ] 2>/dev/null; then
      port=$configured_port
    fi
  fi
  echo "$port"
}

# Ensure worker is running (polls readiness endpoint)
ensure_worker_running() {
  local port="${1:-37777}"
  local retries=30
  local interval=0.2
  local i=0
  while [ $i -lt $retries ]; do
    if run_curl -s --max-time 1 "http://127.0.0.1:${port}/api/readiness" &>/dev/null; then
      return 0
    fi
    sleep $interval
    i=$((i + 1))
  done
  return 1
}

# URL encode a string
url_encode() {
  local string="$1"
  if command -v jq &>/dev/null; then
    local encoded
    encoded=$(printf '%s' "$string" | jq -sRr @uri 2>/dev/null)
    if [ -n "$encoded" ]; then
      echo "$encoded"
      return
    fi
  fi
  echo "$string"
}

# Get project name from workspace root
get_project_name() {
  local workspace_root="${1:-$PWD}"
  # Strip Windows drive prefix if present (C:\, D:\, etc.)
  workspace_root="${workspace_root#*:}"
  # Normalise backslashes
  workspace_root="${workspace_root//\\//}"
  # Remove trailing slash
  workspace_root="${workspace_root%/}"
  # Return basename
  basename "$workspace_root" 2>/dev/null || echo "unknown"
}

# External directory for auto-generated hook files (outside the project repo).
get_external_context_dir() {
  local workspace_root="${1:-$PWD}"
  local norm="${workspace_root//\\//}"
  norm="${norm#*:}"
  norm="${norm#/}"
  norm="${norm//\//-}"
  norm="${norm// /-}"
  norm="${norm//_/-}"
  echo "$(get_user_home)/.cursor/claude-mem/${norm}"
}

# Resolve Cursor CLI (PATH or default Windows install location).
get_cursor_cli() {
  if command -v cursor &>/dev/null; then
    echo "cursor"
    return 0
  fi
  local win_path="${LOCALAPPDATA}/Programs/cursor/resources/app/bin/cursor.cmd"
  if [ -f "$win_path" ]; then
    echo "$win_path"
    return 0
  fi
  return 1
}

# Open context in a separate Cursor window once per conversation (avoids tab jumps in project).
open_context_in_cursor_window() {
  local context_file="$1"
  local conversation_id="$2"
  [ -z "$conversation_id" ] || [ ! -f "$context_file" ] && return 0

  local cursor_cli
  cursor_cli=$(get_cursor_cli) || return 0

  local marker_dir="${context_file%/*}"
  local marker_file="${marker_dir}/.cursor-window-session"
  if [ -f "$marker_file" ]; then
    local last_id
    last_id=$(cat "$marker_file" 2>/dev/null)
    [ "$last_id" = "$conversation_id" ] && return 0
  fi

  "$cursor_cli" --new-window "$context_file" &>/dev/null &
  echo "$conversation_id" > "$marker_file" 2>/dev/null
}

# Remove legacy in-repo rules file from older hook versions.
remove_legacy_context_rule() {
  local workspace_root_norm="$1"
  local legacy_file="${workspace_root_norm}/.cursor/rules/claude-mem-context.mdc"
  [ -f "$legacy_file" ] && rm -f "$legacy_file" 2>/dev/null
}

# Safely extract a JSON field
json_get() {
  local json="$1"
  local field="$2"
  local default="${3:-}"
  local value
  value=$(echo "$json" | jq -r "$field // empty" 2>/dev/null)
  if [ -z "$value" ] || [ "$value" = "null" ]; then
    echo "$default"
  else
    echo "$value"
  fi
}

# Check if a value is empty or null
is_empty() {
  local value="$1"
  [ -z "$value" ] || [ "$value" = "null" ]
}
