# TODO: Sprint 8+9 (testes, CI/CD, secrets, produção, lojas), planeado 2026-10-27

**8A FECHADO (2026-10-27):** CI #62 verde, backend 3437, Playwright 5/5. Review: ver `Sessions/2026-10-27-sprint8a-fecho.md`.

Plano completo: `docs/blueprints/sprint_8/00_plano_sprint_8.md`. Código direto na main; `.md` só para guias.

- [x] Leandro aprova as mudanças em `docs_about_app/` e no NEST (caminho do Sprint 7)
- [x] 8A: cobertura real + lacunas; regras de arquitetura em falta
- [x] 8A: CORS `Idempotency-Key`; bootstrap 1.º superuser; Sentry SPA; `frontend/vercel.json` (CSP Report-Only); apple-touch-icon; sem MSW worker no build; Dockerfile
- [x] 8A: Playwright (signup, convite+1.º login, treino, check-in, checkout sem Stripe)
- [x] 8A: `ci.yml` (cache, cobertura, E2E) e `cd.yml` (migrations manual, EF bundle)
- [x] G8A: CI verde
- [x] 8B: guias por serviço + emails + tabela mestre (13 guias + 00_tabela_mestre.md em docs/blueprints/sprint_8/8B/; execução pelo Leandro por fazer)
- [ ] 8C: runbook, `04_PRODUCTION_CHECKLIST.md`, rollback, Render CIDR
- [x] 8D: plano (`docs/blueprints/sprint_8/8D/00_plano_8D.md`)
- [x] 8D-1: site (CTAs, prerender multi-rota, Privacidade, Termos, Suporte, Eliminar conta)
- [x] 8D-2: sessão por token para a app nativa (rotas `api/v1/native/auth`, sem migration)
- [x] 8D-3: eliminar conta de cliente (anonimizar e manter)
- [x] 8D: documentar o que falta (8D-4 Capacitor, 8D-5 guias das lojas, 8D-6 submissão)
- [x] 8D: validação (suites, mutações, revisão de segurança, site no browser)
- [x] Fecho 8D sem dependências: nota de sessão, ACTIVE, NEST, GOTCHAS, lessons (2026-10-28)

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
