#!/bin/bash
# Blocks obviously dangerous shell commands before execution.
# Used as a PreToolUse hook for the Bash tool.
# Exit 2 = block. Exit 0 = allow.

if ! command -v jq >/dev/null 2>&1; then
  exit 0
fi

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty')
[ -z "$COMMAND" ] && exit 0

deny() {
  local reason="${1//\"/\\\"}"
  echo "{\"hookSpecificOutput\":{\"hookEventName\":\"PreToolUse\",\"permissionDecision\":\"deny\",\"permissionDecisionReason\":\"$reason\"}}"
  exit 2
}

case "$COMMAND" in
  *"rm -rf /"*|*"rm -rf ~"*|*"rm -rf ."*)
    deny "Comando destrutivo bloqueado: apagar diretorios em massa." ;;
  *"git push --force"*|*"git push -f"*)
    deny "Force push bloqueado. Confirma manualmente se e mesmo necessario." ;;
  *"DROP TABLE"*|*"DROP DATABASE"*|*"TRUNCATE"*)
    deny "Comando SQL destrutivo bloqueado. Confirma manualmente." ;;
  *"chmod -R 777"*)
    deny "Permissoes inseguras bloqueadas." ;;
  *":(){ :|:& };:"*)
    deny "Fork bomb bloqueada." ;;
esac

exit 0
