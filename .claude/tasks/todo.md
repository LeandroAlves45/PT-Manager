# TODO — Sessão 2026-09-24 · Planeamento 6E + blueprints da fatia 6E-1

Objetivo: planear a fase 6E inteira (5 fatias) num doc reutilizável e gerar blueprints validados
da 6E-1 (painel, clientes, detalhe+resumo, avaliação inicial, 2º trainer no seed), a partir de
uma worktree descartável `C:\ptm-tmp-sprint-6e`. Nunca merge; implementação real é do Leandro.

Decisões do utilizador: fatias (só 6E-1 agora) · 2º trainer no seed · lista de clientes só com
campos existentes · sem dependências novas.

## Trabalho

- [x] Worktree `C:\ptm-tmp-sprint-6e` (branch `tmp/sprint-6e`) + `git status` do repo real antes
- [x] Doc 00 — plano da fase 6E completa (endpoints/DTOs confirmados no código)
- [x] Backend na worktree: 2º trainer no seed + testes; Postgres descartável `ptm-6e-tmp`
- [x] Frontend na worktree: componentes shared → painel → clientes → detalhe/avaliação
- [x] Testes (src/test/), incl. negativo por papel e isolamento
- [x] Gates: npm ci, lint, typecheck, test, build, format:check, api:types:check, greps, mutação
- [ ] Verificação no browser pane: trainer 1 vs trainer 2 — **não feita**: exige login com
      password, que o agente não faz; passou a `QG6E1-ISOLAMENTO-UI-001` (Leandro). Isolamento
      provado pela API real em `DevelopmentSeedTests.SeededTrainers_SeeOnlyTheirOwnClients`.
- [x] Extrair docs 01–08; diff programático normalizado + SHA-256
- [x] Revisão por agente sonnet do pack vs código
- [x] Descartar worktree, branch e contentor; `git status` do repo real depois
- [x] QualityGates.md, NEST, ACTIVE, Sessions, MEMORY, plan_sprint_6 (obsidian-ptmanager)

## Review

Finalizado (planeamento + blueprints 6E-1). Pack `sprint_6E/` 00–08: doc 00 com a pesquisa da fase
inteira; 33 ficheiros da 6E-1 (31 blocos idênticos + 2 excertos validados). Worktree: 141 testes,
lint/typecheck/build, api:types:check, DevelopmentSeedTests 7/7, 14/14 + 1 mutações; revisão sonnet
sem defeitos. Worktree, branch e contentor apagados; repo real só com `.claude/` (e `docs/`,
ignorado). Backend completo: 1 falha pré-existente dependente da ordem (tarefa separada).
Pendente para o Leandro: aplicar 02–07, reset da BD dev, verificação visual com os 2 trainers.

---

# TODO — Sessão 2026-09-25 · Site de marketing (`site/`)

Plano: `~/.claude/plans/logical-giggling-hanrahan.md`. Decisões: app separada em `site/`,
placeholders removidos, CTAs → `{VITE_APP_URL}/auth/login`, domínios ptmanager.pt / app.ptmanager.pt.

- [x] 1. Scaffold `site/` (configs do frontend adaptadas, versões fixadas, npm install)
- [x] 2. `domain/` `content/` `config/` `seo/` + testes (limites = SubscriptionTier.cs)
- [x] 3. `ui/` + `styles/` + teste de sincronização de tokens com o frontend
- [x] 4. `sections/` (impeccable-ptmanager) + testes RTL
- [x] 5. `app/` entries + `scripts/prerender.mjs` + teste do HTML gerado
- [x] 6. `vercel.json` (CSP/headers) + teste · `.gitignore` raiz · job CI `site`
- [x] 7. Verificação visual browser pane (375/1280) + auditoria design-is
- [x] 8. Revisão de segurança + npm audit
- [x] 9. Remover `landing-page-site/` (após confirmação) · memória/sessão · checklist final

## Review

- 44 testes verdes (8 ficheiros); lint, typecheck, format:check e build (SSR + pré-render) verdes;
  npm audit --omit=dev = 0. Mutações: limite de plano e HSTS ficam vermelhas.
- Browser com a CSP real: sem violações, hidratação OK, teclado OK, 375 px sem overflow.
- Revisão de segurança (agente): sem achados; connect-src apertado para 'none'.
- Copy verificada contra o backend: removida a promessa falsa de pagamentos Stripe aos clientes.
- design-is: 18/30 (REDESIGN pela regra), handoff em site/DESIGN-IS-2026-09-25/04; o movimento 3 foi aplicado.
- Pendente: projeto Vercel (root site/, env vars, domínios); CTA de registo na 6G; commit não pedido.
