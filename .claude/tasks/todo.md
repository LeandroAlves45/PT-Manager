# TODO — Sessão 2026-10-02 · Sprint 6E-5 planeamento e blueprints

Objetivo: planear a 6E-5 (check-ins, definições, subscrição, tab Check-ins), validar numa worktree
descartável `C:\ptm-tmp-6e5` e extrair blueprints de código real para
`docs/blueprints/frontend-files/sprint_6/sprint_6E/sprint_6E5/`. Nunca merge; código real fica a cargo do Leandro.

Decisões: U1 `client_name` em `CheckInResponse`; U2 check-ins completos; U3 aviso de retorno do
Stripe; U4 marca só no formulário (portal aplica na 6F). Sem dependências novas.

- [x] Pesquisa de contratos backend (check-ins, definições, billing) e estado do frontend
- [x] Perguntas de clarificação (U1–U4) e revisão de gaps
- [x] Worktree `C:\ptm-tmp-6e5` + `npm ci`
- [x] Backend: `client_name` em check-ins + testes + `schema.d.ts` regenerado
- [x] Frontend: feature check-ins (página, ações, correção, tab do cliente)
- [x] Frontend: definições (marca, logo, contactos, fuso)
- [x] Frontend: subscrição (checkout/portal, retorno) + corrigir D1 `SubscriptionCard`
- [x] Rotas e integração
- [x] Testes + mutações
- [x] Gates (lint, typecheck, test, build, prettier, api:types:check; backend build/test)
- [x] Extrair pack 01–08 + manifest; round-trip IGUAL
- [x] Apagar worktree e branch; `git status` só docs
- [x] obsidian-ptmanager: sessão, ACTIVE, MEMORY, NEST, 00, QualityGates

## Review

- U5 acrescentado a meio (decisão do Leandro): retoma do Checkout aberto do mesmo plano no
  backend, depois de a regra ESLint proibir `sessionStorage`.
- D1 pré-existente corrigido no pack (cartão da sidebar).
- Worktree: backend 2917 testes (1 skip), frontend 277, 15/15 mutações, `api:types:check` verde,
  round-trip 55/55. Worktrees `C:\ptm-tmp-6e5` e `C:\ptm-tmp-6e5-rt` e branches removidas.
- Repo principal: só `.claude/`, `backlogs/` e `docs/` (ignorado) mudaram.
- Pendente (Leandro): aplicar docs 02–07, URLs de retorno Stripe, gates manuais do doc 08.

