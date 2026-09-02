---
name: sprint-context
description: >
  Contexto de sprint/fase sem reler o repositório inteiro. Usar ao planear,
  gerar blueprints, rever gates ou fechar uma fase. Modos plan, blueprint,
  implement (orientação ao utilizador) e review. Ativa em "planear sprint",
  "fase activa", "sub-lote", "gate", "review da fase", ou quando o utilizador
  referencia ACTIVE.md ou Sprint Pack.
---

# Sprint Context — PT Manager

Evita exploração global. O utilizador implementa manualmente; as IAs planeam,
documentam, testam e revêm.

## Bootstrap obrigatório (todos os modos)

1. `.claude/memory/ACTIVE.md`
2. Sprint Pack indicado em ACTIVE (ex.: `.claude/project/sprints/sprint-4/fase-4/README.md`)
3. `surface.yaml` do mesmo pack, se existir
4. Nota de sessão referenciada em ACTIVE
5. `git status --short`

Só depois: documento concreto em `docs/backend-files/...` ou código nos paths do pack.

**Nunca** usar `graphify-out/` se for anterior ao último fecho de Sprint (ver
`.claude/project/sprints/GRAPHIFY.md`).

## Modos

### `plan` — Claude Code, Codex

**Quando:** planear sub-lote, rever ordem, identificar blockers.

**Ler:** ACTIVE + Sprint Pack README + `00_indice` local + sessão.  
**Evitar:** grep em `backend/` fora de `surface.yaml`.  
**Entregar:** plano com sub-lotes, gates, dependências, decisões em aberto.

### `blueprint` — Claude Code, Codex

**Quando:** gerar ou rever blueprints (código integral em `docs/`).

**Ler:** modo `plan` + `.claude/memory/Patterns/blueprints_codigo_real_por_ficheiro.md`
(ou `blueprints_pseudocodigo_por_ficheiro.md` se pedido).  
**Evitar:** inventar tipos; confirmar assinaturas no código real dos paths listados.  
**Skill complementar:** `graphify-pseudocode` para formato do output.

### `implement` — orientação (utilizador implementa)

**Quando:** o utilizador pergunta como aplicar um doc N.

**Ler:** doc N + ficheiros alvo listados no doc + diff local.  
**Cursor:** dúvidas e ajustes pontuais; não reimplementar o lote inteiro salvo pedido.

### `review` — Claude Code, Codex (review completa); Cursor (review local)

**Quando:** fecho de sub-lote ou fase; após implementação manual.

**Ler:** gate do sub-lote (`03`, `06`, `09`, `13`, `15`) + `backlogs/QualityGates.md`.  
**Executar:** `dotnet test PTManager.sln --configuration Release` (e build se relevante).  
**Evitar:** marcar gate fechado sem evidência de testes.  
**Skills complementares:** `code-review-leandro`, `security-reviewer` se pedido.

## Ferramentas por propósito (convencção do projecto)

| Ferramenta | Papel |
|---|---|
| **Cursor** | Dúvidas, pequenos ajustes, review local rápida |
| **Claude Code** | Planear, blueprints, review completa |
| **Codex CLI** | Planear, blueprints, review completa |

Todas seguem a mesma ordem de leitura (ACTIVE → Sprint Pack).

## Ao fechar sub-lote ou fase

1. Correr testes e registar comando + resultado na nota de sessão.
2. Actualizar `.claude/memory/ACTIVE.md` (sub-lote actual, blockers resolvidos).
3. Se sprint inteiro fechou: regenerar graphify (ver `GRAPHIFY.md`).

## Resolução de conflitos

Precedência igual a `AGENTS.md`. Se blueprint diz «nada implementado» mas `git status`
ou código mostram o contrário, **prevalece o disco**.
