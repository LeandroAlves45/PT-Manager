# security-audit — PT Manager

Skill de **auditoria de segurança** para o monorepo PT Manager:

- **Backend:** ASP.NET Core (.NET 10), EF Core, multi-tenant, Stripe
- **Frontend:** React 19 + Vite 7 (SPA — **sem Next.js**)

Relatórios em **português de Portugal**. Patches são **somente propostas** — nada é aplicado automaticamente.

**Fora de escopo por defeito:** `backend-python/` (referência histórica).

---

## Localização no repositório

A skill está sincronizada em três pastas (conteúdo idêntico):

```
.cursor/skills/security-audit/
.claude/skills/security-audit/
.agents/skills/security-audit/
    ├── README.md
    ├── SKILL.md
    └── refs/
        ├── project-stack.md
        ├── language.md
        ├── aspnet-checklist.md
        ├── react-vite-checklist.md
        ├── aspnet-middleware.md
        ├── secrets.md
        ├── false-positives.md
        ├── security-headers.md
        └── report.md
```

Gatilho automático adicional: `.cursor/rules/security-audit.mdc`

---

## O que esta skill faz

| Etapa | Conteúdo |
| ----- | -------- |
| 1 | Escopo + stack real (lockfiles backend + frontend) |
| 2 | `npm audit` + `dotnet list package --vulnerable` |
| 3 | Segredos (Git tracked, appsettings, CI/CD) |
| 4–5 | Código: injecção, auth, **multi-tenant**, **Billing/Stripe**, frontend |
| 6 | Filtro de falsos positivos |
| 7 | Relatório estruturado |
| 8 | Patches sugeridos só para **CRITICAL** e **HIGH** |

**Auditoria full** inclui sempre checklist **Billing/Stripe** + skill `payment-security`.

---

## Quando usar qual skill

| Pedido | Skill |
|--------|-------|
| Auditoria completa do repo | **security-audit** |
| Review de PR / diff | `security-reviewer` |
| Billing / PCI / webhooks Stripe (detalhe) | `payment-security` |
| Dúvida pontual API auth | `security` |

---

## Como activar

### Automático

O agente tende a carregar quando a mensagem menciona: auditoria de segurança, IDOR, cross-tenant, XSS, webhooks Stripe, JWT, secrets, `/security-audit`.

### Manual (recomendado)

```text
/security-audit
```

```text
/security-audit backend/src/Application/Features/Billing
```

```text
Usa a skill security-audit. Relatório em português. Inclui Billing.
```

```text
Auditar tenant isolation e auth — security-audit
```

---

## Pré-requisitos

- Node.js + `frontend/package-lock.json`
- .NET SDK + `backend/**/packages.lock.json`
- Código acessível no workspace

**Não** é necessário Next.js, `middleware.ts`, nem CSP em `vercel.json` — a skill trata isso com cautela.

---

## O que recebes

1. Tabela resumo (CRITICAL → INFO)
2. Achados por **categoria** (incl. multi-tenant e Billing em full audit)
3. **Stack detectado** (backend + frontend)
4. Dependências (npm + dotnet)
5. Segredos
6. Falsos positivos descartados
7. Patches propostos (CRITICAL/HIGH)

Formato: `refs/report.md`.

---

## Anti-ruído

- Não exigir Next.js / Server Actions / Prisma
- CSP/HSTS ausente → no máximo **INFO**
- CVE inventado ou “actualize o .NET” sem advisory → proibido
- Patch automático no repositório → proibido
- `backend-python/` ignorado por defeito

Detalhes: `refs/false-positives.md`.

---

## Referências do projecto

- `AGENTS.md` — regras transversais e ficheiros protegidos
- `.claude/project/00_ARCHITECTURE.md` — auth, tenant, Stripe
- `.cursor/rules/security.md` — regras de segurança PT Manager
