# TODO — Sessão 2026-09-27 · Blueprints da fatia 6E-2 (sessões e packs)

Objetivo: blueprints de código real validados da 6E-2 (sessões, tipos de pack, packs do cliente,
tab Sessões do detalhe), extraídos de uma worktree descartável `C:\ptm-tmp-sprint-6e2`. Nunca merge;
implementação real é do Leandro. Plano: `00_plano_fase_6E_completa.md` §5.2 + plano aprovado.

Decisões do utilizador: E1 `client_name` no backend · E2 Combobox com pesquisa em `shared/` ·
E3 3 separadores (Agenda · Packs dos clientes · Tipos de pack) · E4 pré-escolher o pack que acaba
primeiro · E5 sem dependências novas · (a meio) corrigir o binding de enums da query na worktree.

## Trabalho

- [x] Worktree `C:\ptm-tmp-sprint-6e2` (branch `tmp/sprint-6e2`) + `git status` do repo real antes
- [x] Backend: `TrainingSessionResponse` + `client_name` (sessões e packs) + testes Release
- [x] API Development na worktree → `schema.d.ts` regenerado; sonda do binding de `status=`
- [x] Binder global de enums da query (`QueryEnumModelBinderProvider`) + testes (pedido a meio)
- [x] Frontend partilhado: FormField movido, popover, tabs, Combobox, Pagination, ClientCombobox
- [x] Feature sessions (api, lib, formulários, ações, agenda, lista)
- [x] Feature packs (tipos de pack, packs do cliente)
- [x] SessionsPage + router + painel + tab Sessões no detalhe
- [x] Testes + lint/typecheck/test/build verdes (171 frontend, 2900 backend)
- [x] Mutações (21/21 frontend + B1 backend)
- [x] Revisão de código (subagente sonnet): 1 defeito corrigido com teste; design-is **não** corrido (sem login no browser)
- [x] Extrair docs 01–08 em `sprint_6E2/`; gerador + leitor independente: 0 falhas; SHA-256
- [x] `git status` do repo real; worktree, branch e contentor apagados
- [x] Memória: sessão, ACTIVE, MEMORY, NEST, doc 00, lessons, QualityGates, memória automática

## Review

Pack `docs/blueprints/frontend-files/sprint_6/sprint_6E/sprint_6E2/` com 8 docs e 71 caminhos.
Cinco defeitos encontrados antes de chegarem ao Leandro: `IgnoreQueryFilters` em subquery (fuga de
filtros), colisão de schema OpenAPI, enums snake_case recusados na query, teste de preço fraco, e
pré-escolha do pack a sobrescrever a escolha manual. Pendente: aplicar 02–07 num só commit,
verificação manual com login (isolamento de dois trainers e inspeção visual), CI após push.
