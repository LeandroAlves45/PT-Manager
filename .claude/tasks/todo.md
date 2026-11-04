# TODO: 8D-4 app nativa + 8D-5 guias (2026-11-04)

Spec: `.claude/SPEC.md`. Branch `feature/8d-app-nativa`.

## Pendente de fases anteriores (Sprint 8+9)
- [ ] 8C runbook (Leandro), revisão e importação de `catalog-data/*.csv`.
- [ ] `QG8DP-PROD-001` verificação manual em produção (Leandro).

## Este trabalho
- [x] Branch, SPEC.md, todo.md, ler `docs_about_app`
- [x] Site: `legalReviewed = true`, `vatNotice`, itálico da Saira (proposta)
- [x] Backend: `auth_time` + reautenticação na eliminação (TDD)
- [x] Backend: política CORS `NativeApp` (TDD)
- [x] Frontend: módulo `shared/native` + integração + testes
- [x] Capacitor: pacotes, config, `android/`, `ios/`, ícones, `vercel.json`
- [x] CI: `mobile.yml`
- [x] Guias 8D-5 (03–07)
- [x] Mutações, revisão de segurança e code review
- [x] Gates `QG8D4-*` em `backlogs/QualityGates.md`
- [x] Push, CI verde, merge
- [x] Documentação (ACTIVE, MEMORY, NEST, sessão, GOTCHAS, lessons), apagar SPEC.md
- [ ] `docs_about_app` (só com aprovação do Leandro)

## Review
- Commits `346dd1d`, `2fbe031`, `589d747`, `2c788d2`; CI #80 verde nos 5 jobs, Mobile #2 verde.
- Mutações 9/9 backend, 15/15 frontend; revisões de segurança sem C/H/M.
- Pendentes do Leandro: QG8D4-IOS-CI-001, -DOCS-001, -DEVICE-001, -LINKS-001.
- **Finalizado** (código); `docs_about_app` à espera de aprovação.
