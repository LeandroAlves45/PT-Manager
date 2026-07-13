param(
  [Parameter(Mandatory = $true)]
  [string]$HookScript
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Get-ProjectDir {
  if (-not [string]::IsNullOrWhiteSpace($env:CLAUDE_PROJECT_DIR)) {
    return (Resolve-Path -LiteralPath $env:CLAUDE_PROJECT_DIR).Path
  }

  return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Read-HookInput {
  $raw = [Console]::In.ReadToEnd()
  if ([string]::IsNullOrWhiteSpace($raw)) {
    return $null
  }

  try {
    return $raw | ConvertFrom-Json
  } catch {
    return $null
  }
}

function Send-Decision {
  param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("deny", "ask")]
    [string]$Decision,
    [Parameter(Mandatory = $true)]
    [string]$Reason
  )

  @{
    hookSpecificOutput = @{
      hookEventName = "PreToolUse"
      permissionDecision = $Decision
      permissionDecisionReason = $Reason
    }
  } | ConvertTo-Json -Compress
  exit 2
}

function Test-PathLike {
  param(
    [string]$Path,
    [string]$Pattern
  )

  return $Path -like $Pattern -or $Path -like "*/$Pattern" -or $Path -like "*\$Pattern"
}

function Invoke-ProtectFiles {
  $inputJson = Read-HookInput
  $filePath = $inputJson.tool_input.file_path
  if ([string]::IsNullOrWhiteSpace($filePath)) { exit 0 }

  $basename = [IO.Path]::GetFileName($filePath)
  $protectedPatterns = @(
    ".env", ".env.*", "*.pem", "*.key", "*.crt", "*.p12", "*.pfx",
    "id_rsa", "id_ed25519", "credentials.json", ".npmrc", ".pypirc",
    "package-lock.json", "yarn.lock", "pnpm-lock.yaml",
    "*.gen.ts", "*.generated.*", "*.min.js", "*.min.css"
  )

  foreach ($pattern in $protectedPatterns) {
    if ($basename -like $pattern) {
      Send-Decision deny "Protected file: $basename matches pattern '$pattern'"
    }
  }

  $normalized = ($filePath -replace "\\", "/").ToLowerInvariant()
  switch -Wildcard ($normalized) {
    ".git/*" { Send-Decision deny "Cannot edit files inside .git/" }
    "*/.git/*" { Send-Decision deny "Cannot edit files inside .git/" }
    "secrets/*" { Send-Decision deny "Cannot edit files inside secrets/" }
    "*/secrets/*" { Send-Decision deny "Cannot edit files inside secrets/" }
    ".env" { Send-Decision deny "Cannot edit .env files" }
    ".env.*" { Send-Decision deny "Cannot edit .env files" }
    "*/.env" { Send-Decision deny "Cannot edit .env files" }
    "*/.env.*" { Send-Decision deny "Cannot edit .env files" }
    ".claude/hooks/*" { Send-Decision deny "Cannot edit hook scripts. These enforce security boundaries." }
    "*/.claude/hooks/*" { Send-Decision deny "Cannot edit hook scripts. These enforce security boundaries." }
    ".claude/settings.json" { Send-Decision ask "Editing settings.json. This controls permissions and hooks. Confirm this change." }
    "*/.claude/settings.json" { Send-Decision ask "Editing settings.json. This controls permissions and hooks. Confirm this change." }
    ".claude/settings.local.json" { Send-Decision ask "Editing settings.local.json. This controls local permissions and hooks. Confirm this change." }
    "*/.claude/settings.local.json" { Send-Decision ask "Editing settings.local.json. This controls local permissions and hooks. Confirm this change." }
    "backend/app/db/migrations/*" { Send-Decision deny "Migrations SQL nao devem ser editadas manualmente. Cria um novo ficheiro numerado em backend/app/db/migrations/" }
    "*/backend/app/db/migrations/*" { Send-Decision deny "Migrations SQL nao devem ser editadas manualmente. Cria um novo ficheiro numerado em backend/app/db/migrations/" }
    "docker-compose.yml" { Send-Decision ask "Editing docker-compose. Infrastructure changes require review." }
    "docker-compose.*.yml" { Send-Decision ask "Editing docker-compose. Infrastructure changes require review." }
    "*/docker-compose.yml" { Send-Decision ask "Editing docker-compose. Infrastructure changes require review." }
    "*/docker-compose.*.yml" { Send-Decision ask "Editing docker-compose. Infrastructure changes require review." }
  }

  exit 0
}

function Invoke-WarnLargeFiles {
  $inputJson = Read-HookInput
  $filePath = $inputJson.tool_input.file_path
  if ([string]::IsNullOrWhiteSpace($filePath)) { exit 0 }

  $normalized = ($filePath -replace "\\", "/").ToLowerInvariant()
  switch -Wildcard ($normalized) {
    "node_modules/*" { Send-Decision deny "Cannot write into node_modules/. Install dependencies via package manager instead." }
    "*/node_modules/*" { Send-Decision deny "Cannot write into node_modules/. Install dependencies via package manager instead." }
    "__pycache__/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "*/__pycache__/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    ".pytest_cache/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "*/.pytest_cache/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "venv/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "*/venv/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    ".venv/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "*/.venv/*" { Send-Decision deny "Cannot write into Python cache or virtualenv directories." }
    "bin/*" { Send-Decision deny "Cannot write into bin/ or obj/. These are generated by build tools." }
    "*/bin/*" { Send-Decision deny "Cannot write into bin/ or obj/. These are generated by build tools." }
    "obj/*" { Send-Decision deny "Cannot write into bin/ or obj/. These are generated by build tools." }
    "*/obj/*" { Send-Decision deny "Cannot write into bin/ or obj/. These are generated by build tools." }
    "dist/*" { Send-Decision deny "Cannot write into build output directories. These are generated by the build process." }
    "*/dist/*" { Send-Decision deny "Cannot write into build output directories. These are generated by the build process." }
    "build/*" { Send-Decision deny "Cannot write into build output directories. These are generated by the build process." }
    "*/build/*" { Send-Decision deny "Cannot write into build output directories. These are generated by the build process." }
  }

  $basename = [IO.Path]::GetFileName($filePath).ToLowerInvariant()
  switch -Wildcard ($basename) {
    "*.wasm" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.so" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.dylib" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.dll" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.exe" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.o" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.a" { Send-Decision deny "Cannot write binary files. These should be compiled, not hand-written." }
    "*.zip" { Send-Decision deny "Cannot write archive files." }
    "*.tar" { Send-Decision deny "Cannot write archive files." }
    "*.tar.gz" { Send-Decision deny "Cannot write archive files." }
    "*.tar.bz2" { Send-Decision deny "Cannot write archive files." }
    "*.tgz" { Send-Decision deny "Cannot write archive files." }
    "*.rar" { Send-Decision deny "Cannot write archive files." }
    "*.7z" { Send-Decision deny "Cannot write archive files." }
    "*.mp4" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.mov" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.avi" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.mkv" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.mp3" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.wav" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.flac" { Send-Decision deny "Cannot write media files. Add these manually outside Claude Code." }
    "*.pyc" { Send-Decision deny "Cannot write compiled bytecode files." }
    "*.pyo" { Send-Decision deny "Cannot write compiled bytecode files." }
    "*.class" { Send-Decision deny "Cannot write compiled bytecode files." }
  }

  exit 0
}

function Invoke-ScanSecrets {
  $inputJson = Read-HookInput
  if (-not $inputJson) { exit 0 }

  $toolName = $inputJson.tool_name
  $content = ""
  if ($toolName -eq "Write") {
    $content = $inputJson.tool_input.content
  } elseif ($toolName -eq "Edit") {
    $content = $inputJson.tool_input.new_string
  } else {
    exit 0
  }

  if ([string]::IsNullOrWhiteSpace($content)) { exit 0 }

  $secretFindings = @()
  if ($content -match "AKIA[0-9A-Z]{16}") { $secretFindings += "AWS access key (AKIA...)" }
  if ($content -match "(?i)(aws_secret_access_key|secret_key)\s*[=:]\s*['""]?[A-Za-z0-9/+=]{40}") { $secretFindings += "AWS secret key" }
  if ($content -match "(ghp_|gho_|ghs_|ghr_|github_pat_)[a-zA-Z0-9_]{20,}") { $secretFindings += "GitHub token" }
  if ($content -match "sk-[a-zA-Z0-9]{20,}") { $secretFindings += "API key (sk-...)" }
  if ($content -match "xox[bpras]-[0-9a-zA-Z-]{10,}") { $secretFindings += "Slack token" }
  if ($content -match "-----BEGIN\s+(RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----") { $secretFindings += "private key block" }
  if ($content -match "(mongodb|postgres|mysql|redis|amqp|smtp)(\+[a-z]+)?://[^:\s]+:[^@\s]+@") { $secretFindings += "connection string with credentials" }
  if (
    $content -match "(?i)(password|secret|token|api_key|apikey|api_secret)\s*[=:]\s*['""][^'""]{8,}['""]" -and
    $content -notmatch "(?i)(password|secret|token|api_key|apikey|api_secret)\s*[=:]\s*['""]?(process\.env|os\.environ|getenv|\$\{|ENV\[|env\()"
  ) {
    $secretFindings += "hardcoded credential"
  }

  if ($secretFindings.Count -gt 0) {
    Send-Decision ask ("Possible secret detected in content: {0}. Review carefully before allowing." -f ($secretFindings -join "; "))
  }

  exit 0
}

function Invoke-BlockDangerousCommands {
  $inputJson = Read-HookInput
  $command = $inputJson.tool_input.command
  if ([string]::IsNullOrWhiteSpace($command)) { exit 0 }

  switch -Regex ($command) {
    "rm\s+-rf\s+(/|~|\.)" { Send-Decision deny "Comando destrutivo bloqueado: apagar diretorios em massa." }
    "git\s+push\s+(-f|--force)" { Send-Decision deny "Force push bloqueado. Confirma manualmente se e mesmo necessario." }
    "DROP\s+(TABLE|DATABASE)|TRUNCATE" { Send-Decision deny "Comando SQL destrutivo bloqueado. Confirma manualmente." }
    "chmod\s+-R\s+777" { Send-Decision deny "Permissoes inseguras bloqueadas." }
    ":\(\)\{\s*:\|:&\s*\};:" { Send-Decision deny "Fork bomb bloqueada." }
  }

  exit 0
}

function Invoke-FormatOnSave {
  $projectDir = Get-ProjectDir
  $inputJson = Read-HookInput
  $filePath = $inputJson.tool_input.file_path
  if ([string]::IsNullOrWhiteSpace($filePath) -or -not (Test-Path -LiteralPath $filePath)) { exit 0 }

  $extension = [IO.Path]::GetExtension($filePath).ToLowerInvariant()
  if ($extension -eq ".py") {
    $ruff = Get-Command ruff -ErrorAction SilentlyContinue
    if ($ruff) { & $ruff.Source format "$filePath" *> $null }
  } elseif ($extension -in @(".ts", ".tsx", ".js", ".jsx", ".json", ".css")) {
    $prettier = Join-Path $projectDir "frontend\node_modules\.bin\prettier.cmd"
    if (Test-Path -LiteralPath $prettier) {
      & $prettier --write "$filePath" *> $null
    } else {
      $npx = Get-Command npx.cmd -ErrorAction SilentlyContinue
      if ($npx) { & $npx.Source --no-install prettier --write "$filePath" *> $null }
    }
  }

  exit 0
}

function Invoke-SessionStart {
  $projectDir = Get-ProjectDir
  Write-Output "=== PT Manager ==="
  Write-Output "Stack: Python 3.12 / FastAPI / SQLModel / PostgreSQL + React 19 / Vite / Tailwind"

  if (Test-Path -LiteralPath (Join-Path $projectDir ".git")) {
    $branch = (& git -C "$projectDir" rev-parse --abbrev-ref HEAD 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($branch) -and $branch -ne "HEAD") {
      Write-Output "Branch atual: $branch"
    }

    $lastCommit = (& git -C "$projectDir" log --oneline -1 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($lastCommit)) {
      Write-Output "Ultimo commit: $lastCommit"
    }
  }

  $memoryFile = Join-Path $projectDir ".claude\memory\MEMORY.md"
  if (Test-Path -LiteralPath $memoryFile) {
    Write-Output ""
    Write-Output "--- MEMORY.md ---"
    Get-Content -LiteralPath $memoryFile -Encoding UTF8
  }

  $todoFile = Join-Path $projectDir "tasks\todo.md"
  if (Test-Path -LiteralPath $todoFile) {
    Write-Output ""
    Write-Output "--- tasks/todo.md ---"
    Get-Content -LiteralPath $todoFile -Encoding UTF8
  }

  Write-Output "========================================"
  exit 0
}

function Invoke-ContextRecovery {
  $projectDir = Get-ProjectDir
  $context = ""

  if (Test-Path -LiteralPath (Join-Path $projectDir ".git")) {
    $branch = (& git -C "$projectDir" rev-parse --abbrev-ref HEAD 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($branch) -and $branch -ne "HEAD") {
      $context = "Branch: $branch"
    }

    $lastCommit = (& git -C "$projectDir" log --oneline -1 2>$null)
    if (-not [string]::IsNullOrWhiteSpace($lastCommit)) {
      $context = "$context | Last commit: $lastCommit"
    }

    $changes = (& git -C "$projectDir" status --porcelain 2>$null | Measure-Object).Count
    if ($changes -gt 0) {
      $context = "$context | Uncommitted changes: $changes files"
    }
  }

  @"
=== CONTEXT RECOVERED AFTER COMPACTION ===

CRITICAL PROJECT RULES (PT Manager)
Backend: Python 3.12, FastAPI, SQLModel, PostgreSQL, JWT, Stripe, APScheduler
Frontend: React 19, Vite, Tailwind, Chakra UI + shadcn/ui
SaaS multi-tenant - roles: superuser, trainer, client

1. LAYERED ARCHITECTURE - MANDATORY
   Ordem: api/routes -> services -> repositories -> db/models
   Routes: HTTP, Depends(auth), delegacao para services
   Services: logica de negocio, sem detalhes HTTP
   Repositories: queries SQLModel, sempre filtrar por trainer_id
   NUNCA colocar logica de negocio em routes
   NUNCA queries directas em routes (usar repositories)

2. MULTI-TENANT - NON-NEGOTIABLE
   Todas as queries filtram por trainer_id do JWT
   Nunca confiar em trainer_id do request body
   Client role so acede aos proprios dados

3. DATABASE MIGRATIONS - NON-NEGOTIABLE
   Migrations SQL em backend/app/db/migrations/
   Aplicar via: python -m app.db.migrate_runner
   NUNCA editar ficheiro SQL ja aplicado
   Nova alteracao = novo ficheiro numerado (NNN_descricao.sql)

4. ERROR HANDLING
   HTTPException nas routes com codigos corretos
   Logging estruturado, Sentry em producao
   Nunca expor stack traces ao cliente

5. TESTING REQUIREMENTS
   pytest no backend (unit + integration)
   Vitest no frontend
   Correr ficheiro especifico apos alteracoes
   Correr antes de marcar tarefa concluida: pytest / npm run test

6. SECURITY
   Secrets em environment variables (API_KEY, SECRET_KEY, STRIPE_*)
   JWT + API Key middleware em routes protegidos
   Stripe webhook: verificar HMAC
   Nunca logar senhas, tokens ou API keys

7. GIT WORKFLOW
   Feature branches, conventional commits
   Testes tem de passar antes de commit

COMMANDS:
  uvicorn app.main:app --reload --port 8000          # Start backend (from backend/)
  python -m app.db.migrate_runner                    # Apply migrations
  pytest                                             # Run backend tests
  ruff check app/ && ruff format app/                # Lint/format Python
  npm run dev                                        # Start frontend (from frontend/)
  npm run test                                       # Run frontend tests
"@

  if (-not [string]::IsNullOrWhiteSpace($context)) {
    Write-Output ""
    Write-Output "Current state: $context"
  }

  $claudeFile = Join-Path $projectDir ".claude\CLAUDE.md"
  if (Test-Path -LiteralPath $claudeFile) {
    Write-Output ""
    Write-Output "=== CLAUDE.md (re-injected) ==="
    Get-Content -LiteralPath $claudeFile -Encoding UTF8
  }

  Write-Output ""
  Write-Output "=== END CONTEXT RECOVERY ==="
  exit 0
}

switch ($HookScript) {
  "protect-files-ADJUSTED.sh" { Invoke-ProtectFiles }
  "warn-large-files-ADJUSTED.sh" { Invoke-WarnLargeFiles }
  "scan-secrets-ADJUSTED.sh" { Invoke-ScanSecrets }
  "block-dangerous-commands.sh" { Invoke-BlockDangerousCommands }
  "format-on-save.sh" { Invoke-FormatOnSave }
  "session-start.sh" { Invoke-SessionStart }
  "context-recovery-ADJUSTED.sh" { Invoke-ContextRecovery }
  default {
    Write-Error "Unsupported Claude hook: $HookScript"
    exit 1
  }
}
