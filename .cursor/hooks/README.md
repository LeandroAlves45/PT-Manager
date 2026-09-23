# Cursor Hooks — claude-mem

Integração de memória persistente entre sessões Cursor via [claude-mem](https://github.com/thedotmack/claude-mem).

## Ligar só quando pedires (2026-09-22)

Os hooks do projeto estão disponíveis, mas **não fazem trabalho** até pedires.
Os hooks globais em `~/.cursor/hooks.json` ficam vazios — senão correm em duplicado.

| Pedido | Efeito |
|---|---|
| `/mem` | Cria o marcador, lê `ACTIVE.md` e ativa a gravação posterior |
| `/mem-off` | Apaga o marcador; os hooks posteriores saem imediatamente |
| Sem pedido | Não existe hook anterior ao envio; custo de arranque nulo |

Marcador (fora do repo): `%USERPROFILE%\.cursor\claude-mem\Projeto_pt_manager\memory-on`

Superpowers: deixar o **plugin desligado**. Pedir com `/superpowers` ou «usa superpowers».

## Hooks Configurados

Ficheiro: [hooks.json](../hooks.json)

| Hook             | Script                | Trigger                                    | Função                                                   |
| ---------------- | --------------------- | ------------------------------------------ | -------------------------------------------------------- |
| Memory control    | `memory-toggle.cmd`  | Manual, através de `/mem` e `/mem-off`            | Ativa ou desativa o marcador sem hook anterior ao envio   |
| Save observation | `save-observation.sh` | `afterMCPExecution`, `afterShellExecution` | Guarda observações                                       |
| Save file edit   | `save-file-edit.sh`   | `afterFileEdit`                            | Regista edições de ficheiros                             |
| Session summary  | `session-summary.sh`  | `stop`                                     | Resume sessão ao terminar                                |

`beforeSubmitPrompt` não está registado. Esta decisão evita bloquear todas as mensagens
durante a inicialização da infraestrutura de hooks do Cursor.

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

- `.claude/memory/ACTIVE.md` — estado mínimo lido no início da sessão
- `.claude/memory/MEMORY.md` — índice consultado apenas quando o pedido precisa de contexto histórico
- `.claude/memory/Sessions/` — notas detalhadas carregadas apenas quando referenciadas

## Ficheiro Auto-Gerado (fora do projeto)

Os hooks escrevem contexto em:

```
~/.cursor/claude-mem/<project-slug>/context.md
```

Exemplo Windows: `C:\Users\<user>\.cursor\claude-mem\Users-...-Projeto-pt-manager\context.md`

**Não fica no repositório.** Abre este ficheiro no VS Code ou noutra janela Cursor para consultar a memória entre sessões. Ficheiros legados em `.cursor/rules/claude-mem-context.mdc` são removidos automaticamente.
