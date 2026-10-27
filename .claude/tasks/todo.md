# TODO: Sprint 8+9 (testes, CI/CD, secrets, produção, lojas), planeado 2026-10-27

Plano completo: `docs/blueprints/sprint_8/00_plano_sprint_8.md`. Código direto na main; `.md` só para guias.

- [ ] Leandro aprova as mudanças em `docs_about_app/` e no NEST (caminho do Sprint 7)
- [ ] 8A: cobertura real + lacunas; regras de arquitetura em falta
- [ ] 8A: CORS `Idempotency-Key`; bootstrap 1.º superuser; Sentry SPA; `frontend/vercel.json` (CSP Report-Only); apple-touch-icon; sem MSW worker no build; Dockerfile
- [ ] 8A: Playwright (signup, convite+1.º login, treino, check-in, checkout sem Stripe)
- [ ] 8A: `ci.yml` (cache, cobertura, E2E) e `cd.yml` (migrations manual, EF bundle)
- [ ] G8A: CI verde
- [ ] 8B: guias por serviço + emails + tabela mestre
- [ ] 8C: runbook, `04_PRODUCTION_CHECKLIST.md`, rollback, Render CIDR
- [ ] 8D: guia das lojas
- [ ] Fecho: nota de sessão, ACTIVE, NEST, GOTCHAS, lessons

---

# TODO (concluído): Auditoria de segurança pré-produção (2026-10-27)

Auditoria estática de `backend/` e `frontend/` antes de produção. Relatório local em
`backlogs/Sprint_7_audit_security/00_relatorio_auditoria_seguranca.md` (a pasta `backlogs/`
não entra no Git).

## Trabalho desta sessão

- [x] Ligar a memória (`.cursor/hooks/memory-toggle.cmd on`)
- [x] Subir `zod` de `4.6.5` para `4.6.7` e regenerar `frontend/package-lock.json`
- [x] Alargar `.gitignore` a chaves, `secrets/`, `credentials/`, `keys/` e `appsettings` de produção/local
- [x] Validar: `npm audit --omit=dev` → `found 0 vulnerabilities` (exit 0)
- [x] Escrever o relatório com os achados e o que foi aplicado
- [x] Actualizar a memória (`ACTIVE.md`, `MEMORY.md`, nota de sessão)

## Fora desta sessão

- CSP da SPA no painel da Vercel (INFO, sem XSS confirmado).
- Confirmação de deploy: ambiente `Production`, CORS HTTPS, proxies, `SameSite`, segredos Stripe/QStash/JWT.
- Suites de testes do frontend e do backend: não correram nesta sessão.

## Review

- **Achados:** CRITICAL 0, HIGH 0, MEDIUM 0, LOW 2 (corrigidos), INFO 1 (CSP, em aberto).
- **Zod:** o advisory GHSA-mrhx-c7xc-m3w5 saiu do audit de runtime. O `npm audit` com devDependencies ainda lista vite, vitest, source-map-js e undici; ficam fora do relatório de produção.
- **`.gitignore`:** `!.env.example` ficou depois de `.env.*`, para os exemplos do frontend e do site continuarem versionáveis.
