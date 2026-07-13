# Cursor Hooks — claude-mem

Integração de memória persistente entre sessões Cursor via [claude-mem](https://github.com/thedotmack/claude-mem).

## Hooks Configurados

Ficheiro: [hooks.json](../hooks.json)

| Hook             | Script                | Trigger                                    | Função                                                   |
| ---------------- | --------------------- | ------------------------------------------ | -------------------------------------------------------- |
| Session init     | `session-init.sh`     | `beforeSubmitPrompt`                       | Inicia sessão no worker                                  |
| Context inject   | `context-inject.sh`   | `beforeSubmitPrompt`                       | Injeta memória em `~/.cursor/claude-mem/<project>/context.md` (fora do repo) |
| Save observation | `save-observation.sh` | `afterMCPExecution`, `afterShellExecution` | Guarda observações                                       |
| Save file edit   | `save-file-edit.sh`   | `afterFileEdit`                            | Regista edições de ficheiros                             |
| Session summary  | `session-summary.sh`  | `stop`                                     | Resume sessão ao terminar                                |

## Requisitos

1. **Worker claude-mem** a correr em `http://127.0.0.1:37777`
2. **jq** e **curl** disponíveis no PATH (Git Bash ou WSL no Windows)
3. **bash** para executar os scripts

## Comportamento sem Worker

Todos os hooks falham graciosamente (`exit 0`) se o worker não estiver disponível. O Cursor continua a funcionar — apenas sem memória entre sessões.

## Verificar Worker

```bash
curl -s http://127.0.0.1:37777/health
```

## Memória Complementar

Além do claude-mem, o projeto mantém memória manual em:

- `.claude/memory/MEMORY.md` — índice e gotchas
- `.claude/memory/gotcha_*.md` — notas individuais
- Abrir estes files ou no VSCode ou window nova do Cursor, nunca no projeto.

## Ficheiro Auto-Gerado (fora do projeto)

Os hooks escrevem contexto em:

```
~/.cursor/claude-mem/<project-slug>/context.md
```

Exemplo Windows: `C:\Users\<user>\.cursor\claude-mem\Users-...-Projeto-pt-manager\context.md`

**Não fica no repositório.** Abre este ficheiro no VS Code ou noutra janela Cursor para consultar a memória entre sessões. Ficheiros legados em `.cursor/rules/claude-mem-context.mdc` são removidos automaticamente.
