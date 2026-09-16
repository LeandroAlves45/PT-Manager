# PT Manager — Frontend (documentação canónica)

Frontend reescrito **do zero** no Sprint 6, contra o contrato HTTP v1 do backend .NET.
O frontend anterior (JSX, Axios com `X-API-KEY` e Bearer em `localStorage`) foi feito
para o backend Python, não é usado em produção e não é migrado: fica apenas no
histórico git.

> **Entrada do Sprint 6:** só depois de o Gate 5D (vídeo privado, Cloudflare R2) estar
> fechado no backend. Ver `../02_SPRINTS_ROADMAP.md`.

## Ordem de leitura

| # | Documento | Para quê |
|---|---|---|
| 0 | [00_ARQUITETURA_FRONTEND.md](00_ARQUITETURA_FRONTEND.md) | Camadas, pastas por feature, fluxo de dados, auth, rotas (diagramas Mermaid) |
| 1 | [01_STACK_E_DEPENDENCIAS.md](01_STACK_E_DEPENDENCIAS.md) | Packages aprovados, excluídos e ressalva de auditoria |
| 2 | [02_CONVENCOES.md](02_CONVENCOES.md) | TypeScript, naming, React Query, formulários, erros, testes |
| 3 | [03_DESIGN_SYSTEM_E_MARCA.md](03_DESIGN_SYSTEM_E_MARCA.md) | Tokens do logo, tema, tipografia, componentes-chave |
| 4 | [04_BENCHMARK_E_FUNCIONALIDADES.md](04_BENCHMARK_E_FUNCIONALIDADES.md) | UpCoach e concorrentes vs o que o backend já suporta |
| — | [design-prompts/claude-design.md](design-prompts/claude-design.md) | Prompt para o Claude Design |
| — | [design-prompts/v0.md](design-prompts/v0.md) | Prompt para o v0 |

Contrato do backend consumido pelo frontend:
[`../backend/01_API_ENDPOINTS.md`](../backend/01_API_ENDPOINTS.md) e
[`../backend/02_CONTRATO_HTTP_FRONTEND.md`](../backend/02_CONTRATO_HTTP_FRONTEND.md).

## Decisões fechadas (2026-09-15)

1. Recomeçar do zero; TypeScript `strict` em todo o código.
2. shadcn/ui + Tailwind CSS v4 apenas. Chakra UI fora.
3. Estado do servidor com TanStack Query; cliente HTTP e tipos gerados do OpenAPI.
4. Endpoints autenticados desde a Fase 6A; só a **página de login final** fica para a 6E
   (na 6A existe um login mínimo de desenvolvimento).
5. Seed de desenvolvimento no backend: superuser, trainer e cliente ligado ao trainer,
   com dados suficientes para ver todas as páginas.
6. Ordem: 6A Fundações → 6B Admin → 6C Trainer → 6D Cliente → 6E Auth UX.
7. Tema com toggle dark/light; marca derivada do logo PT Manager (azul `#00A3E9`).
   Logo SVG e favicons fornecidos pelo utilizador (criados com o Codex).
8. Funcionalidades sem backend (chat, calendário, tarefas, cofre, hábitos, staff) são
   **implementação futura** e não são desenhadas como funcionais.

## Onde ficam os blueprints

Os blueprints com código integral de cada fase do Sprint 6 vão para
`docs/frontend-files/sprint_6/` (gitignored), tal como os do backend em
`docs/backend-files/`.
