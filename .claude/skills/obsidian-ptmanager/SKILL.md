---
name: obsidian-ptmanager
description: Memória persistente do PT Manager em `.claude/memory/`. Usar no início de cada sessão para carregar contexto, e no fim para registar decisões e sessões. Ativa em início de sessão, "carregar memória", "guardar notas da sessão", revisões e retrospetivas. `.codex/memory/MEMORY.md` é só um ponteiro de compatibilidade, sem histórico próprio.
---

# Memória persistente do PT Manager

Gere o diretório `.claude/memory/` deste projeto. É o único índice de memória operacional, partilhado por qualquer agente (Claude Code ou Codex CLI, via `AGENTS.md`). Usar como contexto auxiliar, nunca como segunda especificação.

## Precedência de fontes

Quando houver conflito, aplicar esta ordem, igual à definida em `AGENTS.md`:

1. Pedido explícito do utilizador.
2. `AGENTS.md`.
3. Documentos canónicos em `.claude/project/`.
4. Código atual do repositório.
5. `.claude/memory/MEMORY.md` e notas de sessão em `Sessions/`.

Registar a contradição e corrigir a memória desatualizada em vez de adaptar código a ela.

## Estrutura real

```
.claude/memory/
├── ACTIVE.md    # fase activa — ler antes de MEMORY.md em tarefas de sprint
├── MEMORY.md    # índice, ponto de partida geral
├── Sessions/    # uma nota datada por sessão relevante (YYYY-MM-DD-topico.md)
└── Patterns/    # padrões reutilizáveis documentados (blueprints, convenções)

.claude/project/sprints/   # Sprint Packs versionados (índice por fase)
```

Não inventar pastas que não existem no projeto (ex.: Gotchas/, Architecture/, Corrections/). Se um desses tipos de nota passar a fazer falta, criar a pasta só quando houver o primeiro conteúdo real para lá colocar.

## Início de sessão

1. Ler `.claude/memory/MEMORY.md`.
2. Ler `.claude/memory/ACTIVE.md` se a tarefa for sprint, fase, blueprint ou review.
3. Ler apenas as notas de sessão e padrões relevantes para o pedido actual.
4. Correr `git status --short` antes de planear alterações.

Nunca ler ficheiros protegidos ao carregar contexto.

## Quando escrever memória

Atualizar apenas quando pelo menos uma condição for verdadeira:

1. Um sprint, fase ou marco importante mudou de estado.
2. Uma decisão material de arquitetura, contrato, segurança ou dados foi aprovada.
3. Um padrão reutilizável ou um erro recorrente foi identificado.
4. O utilizador pediu explicitamente uma atualização de memória.

Não criar ficheiro de sessão para conversas de rotina ou verificações de estado já cobertas por uma nota existente.

## Fluxo de escrita

1. Verificar a afirmação contra código, testes, histórico git ou documentos canónicos antes de a registar.
2. Criar uma nota datada em `.claude/memory/Sessions/` só para um marco ou decisão que precise de história própria.
3. Manter o `MEMORY.md` conciso: estado atual, decisões duráveis, limitações e próximo passo.
4. Ligar em vez de copiar — se o mesmo contexto aparecer em duas notas, referenciar a canónica.
5. Nunca declarar um teste como aprovado sem evidência dessa execução ou de uma revisão anterior explicitamente atribuída.

## Regras de conteúdo

Markdown UTF-8, Português de Portugal. Datas, caminhos e commits exatos sempre que confirmados. Nunca guardar credenciais, tokens, connection strings ou conteúdo de ficheiros protegidos. Desde que `.claude/memory/` deixou de estar no `.gitignore`, esta pasta tem histórico git real, usa `git log`/`git blame` sobre as notas quando for útil.
