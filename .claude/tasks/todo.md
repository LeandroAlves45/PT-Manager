# TODO: Bloco pré-Sprint 7 (fecho da 6GB) — **Finalizado** (2026-10-24)

Plano aprovado a 2026-10-22 em `~/.claude/plans/luminous-beaming-wirth.md`. Decisões U84–U99.
Branch `feat/pre-sprint7-block`, base `31f9a69` (main). Instruções em `.claude/SPEC.md`. Resumo em
`docs/blueprints/frontend-files/sprint_6/sprint_6G/sprint_6GB/06_plano_bloco_pre_sprint7.md`; fecho em
`07_relatorio_fecho_bloco_pre_sprint7.md`.

## F0. Arranque
- [x] `docs_about_app`, gotchas, lições e plano da 6GB-CL lidos; código mapeado (3 agentes Sonnet)
- [x] Decisões U84–U99 com o Leandro
- [x] Branch, `todo.md`, `SPEC.md`, `06_plano_bloco_pre_sprint7.md`
- [x] Baseline: backend 3298 + 1 skip; frontend 952/952 em 88 ficheiros

## F1. RN-020 avaliação inicial pelo cliente
- [x] Domain: `SubmittedByClient`, `ReviewedAt`, `MarkReviewed`; migration
- [x] Portal: `GET/POST /portal/my-initial-assessment`; `initial_assessment_pending` no Início
- [x] Guarda `initial_assessment_required` em planos de treino, alimentares, atribuições e check-ins
- [x] Trainer: rever, painel «Avaliações iniciais por rever», `has_initial_assessment`, correção de arquivado recusada
- [x] Testes backend (helper que semeia a avaliação nos testes existentes)
- [x] Frontend: esquema partilhado, cartão + página em 3 passos, avisos nas 4 páginas, painel e botão rever
- [x] Testes frontend, commit

## F2. RN-016 email da véspera
- [x] Scanner na ativação do dispatcher; chave idempotente por sessão, dia e destinatário
- [x] Template do personal trainer; gateway com as duas chaves; revalidação e estado `skipped`
- [x] Lembretes sem exigir subscrição ativa; endpoint só em Development
- [x] Testes backend (11 da véspera); commit no fecho

## F3. RN-005 resto do arquivo
- [x] Arquivar: sessões futuras canceladas, saldo consumido (`ForfeitedAt`), convites anulados, refresh revogados
- [x] Listas ativas sem arquivados; ficha só de leitura
- [x] Middleware de acesso por pedido; `authentication_session_ended`
- [x] Frontend: diálogo, corte de sessão; testes, commit

## F4. RN-014 suplementos do plano e tomas
- [x] `MealPlanSupplement`, `ClientMealPlanSupplementIntake`, migration
- [x] Editor do personal trainer; portal com duas secções, tomas por plano, «Registar tomas» na refeição
- [x] Testes, commit

## F5. RN-013 + RN-018 bloqueio e moderação
- [x] Nota livre; bloqueio de suplementos; bloqueio de treinadores com 7 dias
- [x] Login e corte por pedido com motivo e nota; prazo terminado
- [x] Item bloqueado sai do portal e dos totais; desbloquear tira-o do plano
- [x] Backend: emails (outbox), painel, ficha, aviso no Início; migration aplicada à dev
- [x] Frontend: emails (4), painel «Contas bloqueadas», ficha do superuser, aviso no Início
- [x] Testes, commit

## F6. Verificação e fecho
- [x] Suites completas, lint, typecheck, build, contrato (`api:types:check` verde)
- [x] Mutações
- [x] Browser (arnês MSW e app real)
- [x] `code-review-leandro`, `security-reviewer`, `design-is`
- [x] Relatório 08, `docs_about_app` (anunciado antes), roadmap, gates, gotchas, lições
- [x] Graphify, memória, `SPEC.md` apagado, checklist «Finalizado»

## Estado a 2026-10-24
- Backend F1–F5 completo: 3380 testes verdes + 1 skip, `dotnet format` limpo, 5 migrations aplicadas à dev, snapshot da API regenerado (+12 rotas), `schema.d.ts` regenerado.
- Defeito apanhado pelos testes de ponta a ponta: desbloquear no contexto administrativo não tirava o item dos planos (filtros globais sem tenant). Corrigido, registado em GOTCHAS_BACKEND.
- Commits por fase não separáveis (as fases tocam os mesmos ficheiros): um commit no fecho, com a lista por fase na mensagem.
- Frontend: fatia do portal e da autenticação feita e verde (1010/1010 em 91 ficheiros, lint e typecheck limpos). Falta a fatia do personal trainer e do admin.
- Estado e o que falta: `docs/blueprints/.../sprint_6GB/07_estado_bloco_pre_sprint7.md`; prompt para a conversa nova em `07b_prompt_continuacao_bloco_pre_sprint7.md`. O relatório de fecho passa a ser o 08.

## Fecho a 2026-10-24
- [x] Frontend do personal trainer e do admin (agentes A, B, C), diálogo de arquivar
- [x] Revisões (code-review 3 P2 + P3, security 3 LOW) corrigidas; mutações backend 8/8, frontend 5/5
- [x] Suites: backend 3388 + 1 skip, format limpo; frontend 1078/1078 em 96 ficheiros, lint, typecheck, build
- [x] Arnês MSW (ficheiros temporários apagados); `docs_about_app` atualizado (aprovado); relatório 08 escrito
- [x] Decisões: commits backend + frontend; exercício bloqueado com histórico fica para plano novo (ponto em aberto)
- [x] Compilar e testar a correção D10 (`AccountAccessTemplateRenderer` + 3 testes) e a D11 (`InitialAssessmentWizard`) — parar a API antes
- [x] App real: passos 1–5 de «Falta fazer» no relatório 08 §6 (cliente «Teste Bloco» já criado e convidado)
- [x] `design-is` nos ecrãs novos; roadmap, `QualityGates.md` (`QGPRE7-*`), `01_API_ENDPOINTS.md`
- [x] Gotcha D11 em GOTCHAS_FRONTEND; memória (`obsidian-ptmanager`, nota em `Sessions/`); graphify
- [x] Commits (backend, frontend e docs, com o formato aprovado pelo Leandro); `.claude/SPEC.md` apagado
- [ ] Push e merge para a `main` — **à espera da aprovação do Leandro**; depois, CI verde

## Review
- Bloco **Finalizado**: backend 3388 + 1 skip, frontend 1078 em 96 ficheiros, mutações 8/8 + 5/5, revisões sem P1/HIGH, `design-is` 22/30 REFINE, app real no Chrome.
- Defeitos D1–D11 no relatório 08 §3 e §6; pontos em aberto no §8.
- Commits: backend, frontend e `docs(claude)` na branch `feat/pre-sprint7-block`; merge só com aprovação do Leandro.
