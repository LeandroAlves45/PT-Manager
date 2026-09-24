# NEST — onde vive cada documento do PT Manager

*Criado em 2026-09-20 (Sprint 6C). Índice de navegação: não contém decisões nem código.
Se um caminho aqui deixar de existir, corrige-o aqui em vez de procurar de novo.*

## 1. Entrada de qualquer sessão

| Ordem | Ficheiro | O que dá |
|---|---|---|
| 1 | `.claude/memory/ACTIVE.md` | Fase activa, último fecho, gates abertos, próximo passo |
| 2 | `.claude/memory/MEMORY.md` | Índice das notas de sessão em `Sessions/` |
| 3 | `.claude/tasks/todo.md` | Checklist da sessão em curso |
| 4 | `.claude/tasks/lessons.md` | Armadilhas já pagas (ler sempre antes de gerar blueprints) |
| 5 | `.claude/tasks/correction.md` | Padrões de erro corrigidos pelo utilizador |
| 6 | `AGENTS.md` (raiz) | Regras técnicas transversais, válidas para qualquer agente |
| 7 | `.claude/CLAUDE.md` | Comportamento específico do Claude Code |

## 2. Produto e roadmap

| Ficheiro | O que dá |
|---|---|
| `.claude/project/README.md` | Índice do dossiê de projecto |
| `.claude/project/00_ARCHITECTURE.md` | Clean Architecture, modular monolith, multi-tenancy, deploy |
| `.claude/project/01_DATABASE_SCHEMA.md` | Esquema da base de dados |
| `.claude/project/02_SPRINTS_ROADMAP.md` | Âmbito e gate de cada sprint/fase — **fonte de verdade do âmbito** |
| `.claude/project/03_DEVELOPER_GUIDE.md` | Workflow de desenvolvimento |
| `.claude/project/backend-csharp-checklist.md` | Checklist de estilo C# |
| `backlogs/QualityGates.md` | Registo central de todos os `QG*` |
| `backlogs/Backlog.md`, `backlogs/ImplementaçãoFuturas.md` | Itens diferidos (`DEF-*`) |

## 3. Backend — contrato e endpoints

| Ficheiro | O que dá |
|---|---|
| `.claude/project/backend/01_API_ENDPOINTS.md` | Catálogo de endpoints por módulo |
| `.claude/project/backend/02_CONTRATO_HTTP_FRONTEND.md` | Contrato HTTP tal como o frontend o consome |
| `docs/api/api-surface.v1.txt` | **Snapshot verificado por teste** da superfície v1 (170 operações) |

Verdade última é sempre o código: `backend/src/Api/DependencyInjection.cs` (snake_case),
`backend/src/Api/Http/ApiResultMapper.cs` (ProblemDetails),
`backend/src/Api/Configuration/AuthCookieOptions.cs` (`__Secure-ptm-refresh`),
`backend/src/Api/Controllers/AuthController.cs` (`X-CSRF-Token`),
`backend/src/Api/Configuration/ApiCorsPolicy.cs`,
`backend/src/Api/Authorization/ApiRoleNames.cs` (`superuser`/`trainer`/`client`),
`backend/src/Api/Contracts/Common/PagedResponse.cs` e `PageParameters.cs`.

## 4. Frontend — desenho canónico

| Ficheiro | O que dá |
|---|---|
| `.claude/project/frontend/README.md` | Índice e ordem de leitura |
| `.claude/project/frontend/00_ARQUITETURA_FRONTEND.md` | Camadas `app/features/shared`, fluxo de dados, sessão, rotas, AppShell, anti-padrões |
| `.claude/project/frontend/01_STACK_E_DEPENDENCIAS.md` | Packages aprovados/excluídos e a auditoria obrigatória |
| `.claude/project/frontend/02_CONVENCOES.md` | TypeScript, naming, API, erros, formulários, testes, idiomas, git |
| `.claude/project/frontend/03_DESIGN_SYSTEM_E_MARCA.md` | Marca, tokens, tipografia, componentes, white-label |
| `.claude/project/frontend/04_BENCHMARK_E_FUNCIONALIDADES.md` | Benchmark vs capacidades reais do backend |

### Layout aprovado (Claude Design)

| Ficheiro | Fase que serve |
|---|---|
| `.claude/project/frontend/layout/README.md` | Índice e regra: ler os PNG, não o design nem o PDF |
| `layout/01_RELATORIO_ANALISE.md` | Relatório e decisões 1–8 (fechadas em 6A/6B) |
| `layout/02_APPSHELL_NAVEGACAO.md` | **6C** — sidebar, topbar, navegação por role, ⌘K |
| `layout/03_ECRAS_ADMIN.md` | 6D |
| `layout/04_ECRAS_TRAINER.md` | 6E |
| `layout/05_ECRAS_CLIENTE.md` | 6F |
| `layout/06_ESTADOS_E_TOKENS.md` | **6C** — vazio/loading/erro e mini design system |
| `layout/assets/*.png` | 6 screenshots; falta o artboard 08 (⌘K), que tem especificação textual |

### Assets de marca

`docs/logo_and_svg/`: `logo-codex-transparent.svg` (logo aprovado, "criado com o Codex"),
`logo-chat-gpt-transparent.svg` (alternativa), `pt-manager-favicon.svg`, PNG de origem.
Copiados para `frontend/public/` na 6C. `favicon.ico` e `apple-touch-icon.png` são gerados do SVG.

`docs/design-prompts/`: prompts usados para gerar o layout (`claude-design.md`, `v0.md`).

## 5. Blueprints

Raiz: `docs/blueprints/`.

| Caminho | Conteúdo |
|---|---|
| `docs/blueprints/README.md` | Convenções do formato |
| `docs/blueprints/plan_sprint_6_por_atualizar.md` | Estado do Sprint 6 e ponto de entrada |
| `docs/blueprints/backend-files/sprint_6/sprint_6A/` | Pack 6A (00–14), fechado |
| `docs/blueprints/backend-files/sprint_6/sprint_6B/` | Pack 6B (00–14), fechado |
| `docs/blueprints/backend-files/sprint_concluidos/` | Sprints 4 e 5 |
| `docs/blueprints/frontend-files/sprint_6/sprint_6C/` | **Pack 6C (frontend + backend de suporte)**; fecho em `19_relatorio_fecho_fase_6C.md` |
| `docs/blueprints/frontend-files/sprint_6/sprint_6D/` | **Pack 6D admin (00–05)**; contrato de vídeo, catálogos, moderação, visão geral, testes e gates. Worktree descartável, implementação real pendente |

Padrões de escrita:

- Skill `blueprints-codigo-real` (`.claude/skills/blueprints-codigo-real/`) — núcleo comum
  a backend e frontend, mais `references/backend-csharp.md` e
  `references/frontend-typescript.md` por stack. Substitui, desde 2026-09-21, os antigos
  `Patterns/blueprints_codigo_real_por_ficheiro.md` e `Patterns/blueprints_codigo_real_frontend.md`
  (movidos para `Patterns/_to_delete/`, a apagar).
- `.claude/memory/Patterns/blueprints_pseudocodigo_por_ficheiro.md` — formato alternativo
  (pseudocódigo, continua em vigor, não foi tocado nesta migração)

## 6. Sprint packs

`.claude/project/sprints/README.md`, `sprint-5/`, `sprint-6/README.md` (tabela 6A–6G, gates,
decisões abertas), `.claude/project/sprints/GRAPHIFY.md` (regenerar o grafo no fecho de sprint;
grafo em `graphify-out/`).

## 7. Método de materialização de blueprints — regra permanente

*Decidido pelo utilizador em 2026-09-20. Vale para todas as fases seguintes; não voltar a perguntar.*

Os blueprints são validados numa **pasta temporária, criada no início da sessão e apagada no fim**.
Não existe worktree permanente de blueprints e nunca se faz merge.

```bash
# 1. criar a partir do commit base (traz o código real da fase anterior)
git worktree add C:\ptm-tmp-<fase> -b tmp/blueprints-<fase>

# 2. materializar TODOS os blocos lá dentro e validar
#    frontend: npm ci && npm run lint && npm run typecheck && npm run test -- --run && npm run build
#    backend:  dotnet build -c Release && dotnet test

# 3. extrair os blocos validados para docs/blueprints/... no repositório real

# 4. apagar sem deixar rasto
git worktree remove --force C:\ptm-tmp-<fase>
git branch -D tmp/blueprints-<fase>
```

Regras que acompanham o método:

1. Caminho curto (`C:\ptm-tmp-*`) — evita o limite de 260 caracteres do Windows com `node_modules`.
2. `git status` no repositório real antes e depois: só `docs/`, `.claude/` e `backlogs/` mudam.
   `backend/src`, `backend/tests` e `frontend/` **nunca** são tocados numa sessão de blueprints.
3. Se a fase anterior ainda não tiver commit, ajustar a worktree ao estado real logo a seguir a
   criá-la (ex.: em 2026-09-20 o `frontend/` antigo estava apagado sem commit e teve de ser
   apagado também na worktree).
4. O diff entre bloco e ficheiro materializado é **programático e normalizado** (sem comentários,
   sem diferenças de fim de linha) — lição 6 de `.claude/tasks/lessons.md`.
5. Precedente histórico: `C:\ptm6a` na 6A, removido sem merge.
