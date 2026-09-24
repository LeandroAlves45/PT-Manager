# PT Manager — Frontend (documentação canónica)

Frontend reescrito **do zero** no Sprint 6, contra o contrato HTTP v1 do backend .NET.
O frontend anterior (JSX, Axios com `X-API-KEY` e Bearer em `localStorage`) foi feito
para o backend Python, não é usado em produção e não é migrado: fica apenas no
histórico git.

> **Entrada do Sprint 6:** só depois de o Gate 5D (vídeo privado, Cloudflare R2) estar
> fechado no backend. Ver `../02_SPRINTS_ROADMAP.md`.
>
> **Decisão de 2026-09-16:** o Sprint 6 abre com duas fases de **backend** (6A escrita e
> schema, 6B leituras agregadas) que fecham as lacunas do layout aprovado. O frontend
> começa na **6C**. Ver [layout/](layout/README.md) e
> [`../sprints/sprint-6/README.md`](../sprints/sprint-6/README.md).

## Ordem de leitura

| # | Documento | Para quê |
|---|---|---|
| 0 | [00_ARQUITETURA_FRONTEND.md](00_ARQUITETURA_FRONTEND.md) | Camadas, pastas por feature, fluxo de dados, auth, rotas (diagramas Mermaid) |
| 1 | [01_STACK_E_DEPENDENCIAS.md](01_STACK_E_DEPENDENCIAS.md) | Packages aprovados, excluídos e ressalva de auditoria |
| 2 | [02_CONVENCOES.md](02_CONVENCOES.md) | TypeScript, naming, React Query, formulários, erros, testes |
| 3 | [03_DESIGN_SYSTEM_E_MARCA.md](03_DESIGN_SYSTEM_E_MARCA.md) | Tokens do logo, tema, tipografia, componentes-chave |
| 4 | [04_BENCHMARK_E_FUNCIONALIDADES.md](04_BENCHMARK_E_FUNCIONALIDADES.md) | UpCoach e concorrentes vs o que o backend já suporta |
| 5 | [layout/README.md](layout/README.md) | Layout aprovado (Claude Design), relatório de análise e especificação por ecrã |

Os prompts usados no Claude Design e no v0 não estão versionados. A referência visual
canónica é o projeto do Claude Design indicado em [layout/README.md](layout/README.md); o
app-shell do v0 serviu só de comparação (ver `layout/01_RELATORIO_ANALISE.md`).

Contrato do backend consumido pelo frontend:
[`../backend/01_API_ENDPOINTS.md`](../backend/01_API_ENDPOINTS.md) e
[`../backend/02_CONTRATO_HTTP_FRONTEND.md`](../backend/02_CONTRATO_HTTP_FRONTEND.md).

## Decisões fechadas (2026-09-15)

1. Recomeçar do zero; TypeScript `strict` em todo o código.
2. shadcn/ui + Tailwind CSS v4 apenas. Chakra UI fora.
3. Estado do servidor com TanStack Query; cliente HTTP e tipos gerados do OpenAPI.
4. Endpoints autenticados desde a Fase 6C; só a **página de login final** fica para a 6G
   (na 6C existe um login mínimo de desenvolvimento).
5. Seed de desenvolvimento no backend: superuser, trainer e cliente ligado ao trainer,
   com dados suficientes para ver todas as páginas.
6. Ordem: 6C Fundações → 6D Admin → 6E Trainer → 6F Cliente → 6G Auth UX.
7. Tema com toggle dark/light; marca derivada do logo PT Manager (azul `#00A3E9`).
   Logo SVG e favicons fornecidos pelo utilizador (criados com o Codex).
8. Funcionalidades sem backend (chat, calendário, tarefas, cofre, hábitos, staff) são
   **implementação futura** e não são desenhadas como funcionais.

## Decisões de layout (2026-09-16)

1. Layout base: Claude Design ("PT Manager Layout System", 13 artboards). Do v0 entra só
   o dropdown de perfil na topbar.
2. Categoria de alimento e notas de moderação/fonte: **excluídas**.
3. Importação CSV, notificações in-app e endpoint de pesquisa transversal: **futuro**
   (DEF-PROD-004 a 006).
4. Antes do frontend, o backend ganha (6A/6B): RPE, porção padrão, check-in revisto,
   registo de séries e tomas pelo cliente, dashboard do trainer com vendas de packs
   estimadas, resumo do cliente com adesão calculada, fila de moderação, treino de hoje.

## Onde ficam os blueprints

Os blueprints com código real das fases de frontend (6C–6G) ficam em
`docs/blueprints/frontend-files/sprint_6/` e os das fases de backend (6A–6B) em
`docs/blueprints/backend-files/sprint_6/` (ambos ignorados pelo Git). O ponto de
entrada para sessões futuras é `.claude/memory/NEST.md`.
