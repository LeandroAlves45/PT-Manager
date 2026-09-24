# TODO — Sessão 2026-09-24 · Fecho da Sprint 6D (frontend admin)

Objetivo: rever packs 00–03 aplicados no repo real, materializar testes do pack 04, fechar
gates (05), acrescentar a gestão de vídeo do admin (pack 06, decisão do utilizador), relatório,
memória, push e CI verde. Backend da 6D intocado.

## Trabalho

- [x] Diff programático blueprint ↔ código real (01, Breadcrumbs, schema idênticos por SHA-256)
- [x] Baseline: npm ci, lint, typecheck, 83 testes verdes
- [x] Corrigir D1 porção máx. 100 g, D2 rótulo de substituição em curso, D3 motivo de moderação
      persistente, D4 mensagens Zod em inglês, D5 estado bruto do vídeo no formulário
- [x] Pack 06: upload XHR com progresso/cancelar, polling, reprodução, remoção com confirmação
- [x] Testes pack 04 + lacunas + painel de vídeo (112 testes, 18 ficheiros)
- [x] Mutação dirigida (13)
- [x] Gates completos: lint, typecheck, test:run, build, format:check, audit
- [x] Docs: 05 gates, QualityGates.md, 06 vídeo, 07 relatório "Finalizado"
- [x] Memória obsidian-ptmanager
- [x] Commit, push, CI 3/3 verde

## Review

13/13 mutações; 112 testes; CI run #9 verde 3/3 sobre `9319c2f`. Fase 6D Finalizada. Pendentes externos: inspeção manual com login real e R2 real.
