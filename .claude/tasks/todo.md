# TODO: Sprint 7 — plano geral + blueprints 7A (Observabilidade)

Plano aprovado a 2026-10-25 em `~/.claude/plans/pasted-content-id-f2ba-goal-planear-ticklish-dahl.md`.
Decisões S7-01..S7-12. Plano geral: `docs/blueprints/sprint_7/00_plano_sprint_7.md`.
Pack: `docs/blueprints/sprint_7/sprint_7A/`. Worktree descartável `C:\ptm-tmp-7a` (base `11de0fb`).

## F0. Arranque
- [x] Docs lidos (roadmap §7, arquitetura §12, ACTIVE, NEST, gates, gotchas, lições, docs_about_app)
- [x] Versões pesquisadas (Sentry 6.12.1, OTel 1.20.1, Npgsql.OpenTelemetry 10.0.3)
- [x] Destino decidido: Sentry (erros) + Grafana Cloud (traces e métricas), região UE
- [x] `00_plano_sprint_7.md`
- [x] Correções no roadmap §7, arquitetura §12.2 e ACTIVE

## F1. Worktree
- [x] `git worktree add C:\ptm-tmp-7a -b tmp/blueprints-7a 11de0fb`; baseline do build e dos testes
- [x] Pacotes + locks + `--vulnerable`
- [x] Opções `Observability` e `Sentry` com validação
- [x] Logs: scopes + trace id
- [x] Traces: ASP.NET Core, HttpClient, Npgsql, jobs; correlação do job = trace id
- [x] Métricas `PTManager` + db_commands por pedido
- [x] Sentry com scrubbing

## F2. Testes
- [x] Sem segredos (logs, spans, eventos Sentry)
- [x] Métricas emitidas
- [x] Exporter em baixo não parte o negócio
- [x] Mutações
- [x] Build Release `--no-incremental` 0 avisos + suite completa

## F3. Medição
- [x] otel-lgtm + k6 com dados gerados; p95 e queries por rota
- [x] Queries PromQL dos alertas validadas

## F4. Blueprints
- [x] Revisão security + code-review
- [x] Extração por script + manifest + round-trip
- [x] Docs 01–11

## F5. Fecho
- [x] Worktree e branch apagadas; `git status` real só com docs/.claude/backlogs
- [x] docs_about_app (S7-08), QualityGates `QG7A-*`
- [x] Memória (NEST, ACTIVE, Sessions, MEMORY), lições

## Review

**Finalizado (2026-10-25).**
- Plano geral `docs/blueprints/sprint_7/00_plano_sprint_7.md` e pack `sprint_7A/01–10` +
  `manifest.json`: 45 caminhos, round-trip 45/45.
- Worktree `C:\ptm-tmp-7a` (base `11de0fb`, testado `7f0629e`), apagada:
  - 3388 → 3419 + 1 skip, 0 avisos, format e `--vulnerable` limpos;
  - mutações finais 25/25;
  - carga k6 com otel-lgtm.
- Defeitos D1–D4 corrigidos. Revisões de segurança e de código com os achados válidos aplicados.
- Docs atualizados: roadmap §7, arquitetura §12.2, checklist e `docs_about_app` (S7-08, com o
  ADR-001). Gates `QG7A-*` em `backlogs/QualityGates.md`. Memória atualizada.
- **Falta (Leandro):** aplicar o pack, CI, contas EU, gates manuais e alertas. Depois, planear o 7B.
