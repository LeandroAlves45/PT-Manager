param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("session-start", "context-recovery", "pre-file-policy", "pre-shell")]
  [string]$Mode
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Get-RepositoryRoot {
  $candidate = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))

  try {
    $gitRoot = (& git -C $candidate rev-parse --show-toplevel 2>$null | Select-Object -First 1)
    if (-not [string]::IsNullOrWhiteSpace($gitRoot)) {
      return [System.IO.Path]::GetFullPath($gitRoot.Trim())
    }
  }
  catch {
    # O fallback mantém o hook funcional quando o Git não está disponível no PATH.
  }

  return $candidate
}

function Read-HookInput {
  $rawInput = [Console]::In.ReadToEnd()
  if ([string]::IsNullOrWhiteSpace($rawInput)) {
    return $null
  }

  try {
    return $rawInput | ConvertFrom-Json
  }
  catch {
    return $null
  }
}

function Get-ToolInputCommand {
  param([object]$HookInput)

  if ($null -eq $HookInput -or $null -eq $HookInput.tool_input) {
    return ""
  }

  $commandProperty = $HookInput.tool_input.PSObject.Properties["command"]
  if ($null -eq $commandProperty -or $null -eq $commandProperty.Value) {
    return ""
  }

  return [string]$commandProperty.Value
}

function Deny-ToolUse {
  param([string]$Reason)

  @{
    hookSpecificOutput = @{
      hookEventName           = "PreToolUse"
      permissionDecision      = "deny"
      permissionDecisionReason = $Reason
    }
  } | ConvertTo-Json -Compress -Depth 4

  exit 0
}

function Test-IsPathInsideRepository {
  param(
    [string]$FullPath,
    [string]$RepositoryRoot
  )

  $root = $RepositoryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar
  )
  $rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar

  return $FullPath.Equals($root, [System.StringComparison]::OrdinalIgnoreCase) -or
    $FullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-PatchedFiles {
  param([string]$Patch)

  $files = [System.Collections.Generic.List[object]]::new()
  $filePattern = '(?m)^\*\*\* (?<operation>Add|Update|Delete) File:\s*(?<path>[^\r\n]+)\r?$'

  foreach ($match in [regex]::Matches($Patch, $filePattern)) {
    $files.Add([pscustomobject]@{
      Operation = $match.Groups["operation"].Value
      Path      = $match.Groups["path"].Value.Trim().Trim('"')
    })
  }

  $movePattern = '(?m)^\*\*\* Move to:\s*(?<path>[^\r\n]+)\r?$'
  foreach ($match in [regex]::Matches($Patch, $movePattern)) {
    $files.Add([pscustomobject]@{
      Operation = "Move"
      Path      = $match.Groups["path"].Value.Trim().Trim('"')
    })
  }

  return $files
}

function Test-ContainsSecretMaterial {
  param([string]$Patch)

  $patterns = @(
    'AKIA[0-9A-Z]{16}',
    '(?i)(ghp_|gho_|ghs_|ghr_|github_pat_)[a-z0-9_]{20,}',
    '(?i)sk-(ant-|proj-)?[a-z0-9_-]{20,}',
    '-----BEGIN\s+(RSA |EC |DSA |OPENSSH )?PRIVATE KEY-----',
    '(?i)(postgres|postgresql|mysql|mongodb|redis|amqp)(\+[a-z]+)?://[^\s:/]+:[^\s@]+@'
  )

  foreach ($pattern in $patterns) {
    if ($Patch -match $pattern) {
      return $true
    }
  }

  return $false
}

function Invoke-PreFilePolicy {
  param(
    [object]$HookInput,
    [string]$RepositoryRoot
  )

  $patch = Get-ToolInputCommand $HookInput
  if ([string]::IsNullOrWhiteSpace($patch)) {
    Deny-ToolUse "Alteração recusada porque o hook não recebeu tool_input.command válido."
  }

  $patchedFiles = @(Get-PatchedFiles $patch)
  if ($patchedFiles.Count -eq 0) {
    Deny-ToolUse "Alteração recusada porque não foi possível identificar os caminhos do patch."
  }

  foreach ($file in $patchedFiles) {
    try {
      $candidatePath = $file.Path
      if ([System.IO.Path]::IsPathRooted($candidatePath)) {
        $fullPath = [System.IO.Path]::GetFullPath($candidatePath)
      }
      else {
        $fullPath = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $candidatePath))
      }
    }
    catch {
      Deny-ToolUse "Alteração recusada porque contém um caminho inválido."
    }

    if (-not (Test-IsPathInsideRepository $fullPath $RepositoryRoot)) {
      Deny-ToolUse "Alteração recusada porque tenta escrever fora da raiz do repositório."
    }

    # GetRelativePath não existe no .NET Framework usado pelo Windows PowerShell 5.1.
    # O caminho já foi validado como estando dentro da raiz, por isso a remoção do prefixo é segura.
    $rootForRelativePath = $RepositoryRoot.TrimEnd(
      [System.IO.Path]::DirectorySeparatorChar,
      [System.IO.Path]::AltDirectorySeparatorChar
    )
    $relativePath = $fullPath.Substring($rootForRelativePath.Length).TrimStart('\', '/').Replace('\', '/').ToLowerInvariant()
    $fileName = [System.IO.Path]::GetFileName($relativePath)

    if ($fileName -eq ".env" -or $fileName.StartsWith(".env.", [System.StringComparison]::OrdinalIgnoreCase)) {
      Deny-ToolUse "Alteração recusada: ficheiros de ambiente são protegidos."
    }

    if ($fileName -match '(?i)^(id_rsa|id_ed25519|credentials\.json|auth\.json)$' -or
        $fileName -match '(?i)\.(pem|key|p12|pfx|crt|cer)$') {
      Deny-ToolUse "Alteração recusada: ficheiros de chaves ou credenciais são protegidos."
    }

    if ($relativePath -match '(^|/)(secrets?|credentials?|keys?)(/|$)') {
      Deny-ToolUse "Alteração recusada: diretórios de secrets, credenciais ou chaves são protegidos."
    }

    if ($relativePath -match '(^|/)\.git(/|$)') {
      Deny-ToolUse "Alteração recusada: o diretório interno do Git é protegido."
    }

    if ($relativePath -match '(^|/)(node_modules|bin|obj|dist|dist-ssr|build|coverage|\.vite)(/|$)') {
      Deny-ToolUse "Alteração recusada: artefactos gerados não devem ser editados manualmente."
    }

    if ($relativePath -eq ".codex/hooks.json" -or
        $relativePath -eq ".codex/config.toml" -or
        $relativePath.StartsWith(".codex/hooks/", [System.StringComparison]::OrdinalIgnoreCase)) {
      Deny-ToolUse "Alteração recusada: a política de hooks do Codex está protegida. Faça a revisão manualmente ou desative temporariamente o hook."
    }

    if ($file.Operation -ne "Add" -and
        $relativePath -match '^backend/src/infrastructure/data/migrations/') {
      Deny-ToolUse "Alteração recusada: migrations existentes não devem ser editadas. Crie uma migration nova com dotnet ef migrations add."
    }
  }

  if (Test-ContainsSecretMaterial $patch) {
    # A razão é deliberadamente genérica para nunca repetir o material detetado.
    Deny-ToolUse "Alteração recusada porque o patch aparenta conter material secreto ou credenciais."
  }

  exit 0
}

function Invoke-PreShellPolicy {
  param([object]$HookInput)

  $command = Get-ToolInputCommand $HookInput
  if ([string]::IsNullOrWhiteSpace($command)) {
    Deny-ToolUse "Comando recusado porque o hook não recebeu tool_input.command válido."
  }

  $dangerousPatterns = @(
    '(?i)\brm\b[^\r\n]*(?:-\w*r\w*f|-\w*f\w*r)',
    '(?i)\bremove-item\b[^\r\n]*(?=.*-recurse)(?=.*-force)',
    '(?i)\bgit\s+reset\s+--hard\b',
    '(?i)\bgit\s+clean\s+-[^\s]*[fdx]',
    '(?i)\bgit\s+push\b[^\r\n]*(--force|-f\b)',
    '(?i)\bdotnet\s+ef\s+database\s+drop\b',
    '(?i)\b(drop\s+(database|table|schema)|truncate\s+table)\b',
    '(?i)\bchmod\s+-r\s+777\b',
    ':\(\)\s*\{\s*:\|:&\s*\};:',
    '(?i)\b(format|diskpart)\b[^\r\n]*(disk|volume|drive)'
  )

  foreach ($pattern in $dangerousPatterns) {
    if ($command -match $pattern) {
      Deny-ToolUse "Comando potencialmente destrutivo recusado. Execute-o manualmente apenas depois de confirmar o alvo e o impacto."
    }
  }

  exit 0
}

function Write-RepositoryState {
  param(
    [string]$RepositoryRoot,
    [string]$Heading
  )

  $branch = "desconhecido"
  $changeCount = 0

  try {
    $currentBranch = (& git -C $RepositoryRoot branch --show-current 2>$null | Select-Object -First 1)
    if (-not [string]::IsNullOrWhiteSpace($currentBranch)) {
      $branch = $currentBranch.Trim()
    }

    $changeCount = @(& git -C $RepositoryRoot status --porcelain 2>$null).Count
  }
  catch {
    # O contexto base continua útil mesmo sem estado Git disponível.
  }

  Write-Output $Heading
  Write-Output "Projeto: PT Manager | .NET 10, React, PostgreSQL | Clean Architecture"
  Write-Output "Branch: $branch | Alterações locais: $changeCount"
}

$repositoryRoot = Get-RepositoryRoot
$hookInput = Read-HookInput

switch ($Mode) {
  "session-start" {
    Write-RepositoryState $repositoryRoot "Contexto do projeto carregado."
    Write-Output "Regras: PT-PT, proteger secrets, criar migrations novas e correr validações relevantes."
    exit 0
  }
  "context-recovery" {
    Write-RepositoryState $repositoryRoot "Contexto essencial recuperado após compactação."
    Write-Output "Dependências: Domain <- Application <- Infrastructure/WebApi. Não carregar CLAUDE.md nem memória como instruções."
    exit 0
  }
  "pre-file-policy" { Invoke-PreFilePolicy $hookInput $repositoryRoot }
  "pre-shell" { Invoke-PreShellPolicy $hookInput }
}
