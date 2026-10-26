# TODO: Sprint 7B — decisão do Gate 7B (cache e rate limiting distribuídos) + fecho do Sprint 7

Plano aprovado a 2026-10-26 em `~/.claude/plans/pasted-content-id-135c-c-users-leandro-abundant-floyd.md`.
Base: `main` em `f506414`. Pasta: `docs/blueprints/sprint_7/sprint_7B/`. Worktree descartável `C:\ptm-tmp-7b`.

Decisões desta sessão:
- O Leandro decide `Implementar` / `Não implementar ainda`. Só há blueprints de cache com
  `Implementar`.
- A cache mede-se com HybridCache + Redis em Docker; a consolidação mede-se em duas versões (A: LINQ
  juntado; B: SQL único).
- Frescura máxima de 30 s. Dois cenários de latência com toxiproxy.
- .NET 10.0.14 em qualquer caso: nos blueprints com `Implementar`, no checkout principal com
  `Não implementar ainda`.
- Fecho completo do Sprint 7, graphify incluído.

## F0. Arranque
- [x] Docs lidos (00_plano, 7A/08 e 11, arquitetura §6.4/§8/§9.1/§12/§13, roadmap §7, gates, gotchas, lições, docs_about_app)
- [x] Pesquisa curta de pacotes (Hybrid 10.11.0, StackExchangeRedis 10.0.14, SE.Redis 3.4.20, .NET 10.0.14)
- [x] Worktree `C:\ptm-tmp-7b` (`tmp/ptm-7b`)

## F1. Patch .NET 10.0.14 na worktree
- [x] SDK 10.0.403 disponível
- [x] Pacotes, global.json, Dockerfile, locks; build Release; suite completa
- [x] CVEs do 10.0.13/14 lidos

## F2. Baseline k6
- [x] Postgres 5440, otel-lgtm, API Release, k6 EXTRA_CLIENTS=40
- [x] Números por rota contra o §9; sinais 1, 2, 3

## F3. Protótipos
- [x] A: LINQ juntado
- [x] B: SQL único
- [x] C: ITrainerDashboardCache (HybridCache + Redis), invalidação por todas as escritas
- [x] Equivalência dos DTO + testes existentes do dashboard

## F4. Medição comparativa
- [x] 0 ms e atraso de rede (toxiproxy) para original, A, B e C
- [x] C: hit L1/L2/miss, L1 frio, Redis parado, comandos Redis por pedido e em repouso

## F5. Análise
- [x] Rate limiting por policy
- [x] Consumidores (sinal 4)
- [x] Privacidade (L2 no Upstash)

## F6. Decisão
- [x] `sprint_7B/01_decisao_gate_7B.md`, §10 do 00_plano, NEST
- [x] **PARAR**: decisão do Leandro (D1/D2 não implementar; D4, D5 e 10.0.14 na `main`; D3 em aberto)

## F7. Ajustes na `main` (SPEC `sprint_7B/02_spec_ajustes_7B.md`, sem commit)
- [x] S1 .NET 10.0.14 (cherry-pick sem commit do patch validado na worktree)
- [x] S2 `Database.Command: Warning` em produção + `ProductionLoggingConfigurationTests`
- [x] S3 OTLP `http/protobuf` no código + `OtlpExportProtocolTests` (coletor falso)
- [x] Mutações MS2-prod, MS2-dev, MS3-grpc, MS3-default: 4/4 mortas, ficheiros repostos
- [x] Build Release (0 avisos), suite 3423 + 1 skip, format limpo, vulnerable limpo
- [x] Análise de segurança da opção B (doc 01 §14) e custos do Render/Neon (doc 01 §15)

## F8. Fecho
- [x] docs_about_app (RNF-013, ADR-002 + índice + linha 8 do architecture.md, ADR-001, R-006, R-007, product-vision)
- [x] QG7B no QualityGates, roadmap (resultado do Gate 7B), arquitetura §8.1/§12.2/§13.1, lições/gotchas
- [x] Graphify (20 736 nós), memória (ACTIVE, MEMORY, sessão, memória automática), checklist do Sprint 7
- [x] Worktree apagada (`C:\ptm-tmp-7b` e ramo `tmp/ptm-7b`; contentores de medição removidos)
- [x] Commit do código `1898e70`, push, CI #58 verde nos 4 jobs

## Checklist de fecho do Sprint 7 — **Finalizado** (2026-10-26)

| Item | Estado |
|---|---|
| 7A: observabilidade aplicada, CI #56 verde | Finalizado |
| 7B: decisão do Gate 7B registada com métricas e consumidores (ADR-002) | Finalizado |
| 7B: ajustes na `main` (.NET 10.0.14, OTLP, logs de SQL), CI #58 verde | Finalizado |
| Gates `QG7B-*` no QualityGates | Finalizado |
| docs_about_app, roadmap, arquitetura | Finalizado |
| Lições e gotchas consolidados | Finalizado |
| Graphify regenerado | Finalizado |
| Memória (ACTIVE, MEMORY, sessão) | Finalizado |
| Gates manuais do 7A (Grafana eu-west, E2E, alertas) | **Aberto**, depende das contas reais do Leandro |
| D3: consolidar o dashboard | Não feito; reavaliar no 10B |

## Review

- **Decisão:** `Não implementar ainda` à cache, ao Redis e ao rate limiting distribuído.
  - Medido com protótipos reais numa worktree descartável, com rede simulada e falhas do Redis.
  - Nenhum sinal o justifica com uma só instância.
  - A invalidação seria frágil (~30 escritas).
- **Achados com valor fora do âmbito:**
  - o OTLP não exportava nada (gRPC por omissão);
  - o .NET 10.0.12 tinha CVEs que o `--vulnerable` não vê;
  - o p95 do 7A estava inflacionado pelos buckets;
  - os logs de SQL estavam ligados em produção;
  - o graphify incremental perdia ficheiros.
- **Fricção:**
  - ruído da máquina (um instalador);
  - reincidência do `node -e` com escapes;
  - o `gh` não está instalado (CI pela API REST).
