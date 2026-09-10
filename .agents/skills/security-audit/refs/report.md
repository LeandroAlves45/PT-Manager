# Formato do relatório — Security Audit (PT Manager)

Usar no **Step 7**. Idioma: **português de Portugal**. Termos OWASP/CVE em inglês quando padrão.

---

## Cabeçalho

```
╔══════════════════════════════════════════════════════════╗
║           🔐 RELATÓRIO DE AUDITORIA DE SEGURANÇA        ║
║           Skill: security-audit (PT Manager)            ║
╚══════════════════════════════════════════════════════════╝

Projeto:     PT Manager
Data:        <data>
Escopo:      <paths — ex.: backend/, frontend/, full>
Stack detectado (instalado):
  Backend:   net10.0, EF Core <versão lockfile>
  Frontend:  react@<lockfile>  vite@<lockfile>  (SPA — sem Next.js)
  Auth:      JWT + refresh HttpOnly + ASP.NET Identity
  Billing:   Stripe (backend) — secção incluída
Excluídos:   backend-python/, node_modules/, bin/obj/, *Tests*
```

---

## 1. Resumo executivo (obrigatório — primeiro bloco)

```
┌────────────────────────────────────────────────┐
│           RESUMO DE ACHADOS                    │
├──────────────┬─────────────────────────────────┤
│ 🔴 CRITICAL  │  <n>                           │
│ 🟠 HIGH      │  <n>                           │
│ 🟡 MEDIUM    │  <n>                           │
│ 🔵 LOW       │  <n>                           │
│ ⚪ INFO      │  <n>                           │
├──────────────┼─────────────────────────────────┤
│ TOTAL        │  <n>                           │
└──────────────┴─────────────────────────────────┘

Dependências:  npm audit <n> CVE | dotnet vulnerable <n>
Segredos:      <n> credenciais tracked
Billing:       <n> achados Stripe (se full audit)
Cross-tenant:  <n> achados IDOR/tenant
Falsos positivos descartados: <n>
```

Se zero CRITICAL–LOW:

```
✅ Nenhuma vulnerabilidade confirmada nos critérios desta auditoria.
   Escopo: <detalhar>. Limitações: análise estática, sem DAST.
```

---

## 2. Card de achado (por categoria)

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🟠 HIGH — IDOR / Cross-tenant (Autorização)
Confiança: HIGH
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

📍 Local:  backend/src/Application/Features/Clients/GetClient/GetClientHandler.cs:42

🔍 Código:
  var client = await store.GetByIdAsync(request.ClientId, ct);
  // ClientId da route sem validar tenant efectivo

⚠️  Risco:
  Trainer A pode ler cliente do Trainer B passando UUID na URL.

✅ Correção:
  Filtrar por ITenantContext.TrainerId ou ownership; teste cross-tenant negativo.

📚 Referência: OWASP API1:2023 — Broken Object Level Authorization
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

Campos obrigatórios: severidade, confiança, local, snippet, risco, correção, referência OWASP/CWE.

Agrupar por categoria: Injeção, Auth, Multi-tenant, Dados, Crypto, Billing, Config, Frontend.

---

## 3. Auditoria de dependências

```
📦 DEPENDÊNCIAS
════════════════

Frontend (npm audit --omit=dev):
  🟠 HIGH — <pacote>@<versão> — <CVE/GHSA>

Backend (dotnet list package --vulnerable):
  🟠 HIGH — <pacote>@<versão> — <advisory>

⚪ INFO — react@<instalado> (registry: <latest>) — sem CVE
```

---

## 4. Scan de segredos

```
🔑 SEGREDOS E EXPOSIÇÃO
═══════════════════════

🔴 CRITICAL — <descrição>
  Ficheiro: <path>:<linha>
  Acções: rotacionar, remover, .gitignore, git log se pushed
```

Se nada: `✅ Nenhum segredo de alto risco em ficheiros tracked.`

---

## 5. Billing / Stripe (auditoria full)

```
💳 BILLING / STRIPE
═══════════════════

[ ] Raw body preservado
[ ] Assinatura Stripe-Signature validada
[ ] Dedup event.id
[ ] Idempotência checkout/subscription
[ ] Metadata não autoriza sozinha
[ ] Outbox / retry comportamento

Achados: <lista ou ✅ checklist OK>
Referência: payment-security + 00_ARCHITECTURE.md §10
```

---

## 6. Propostas de patch (CRITICAL + HIGH)

```
🛠️  PROPOSTAS DE PATCH
══════════════════════
⚠️  Revise cada patch antes de aplicar. Nada foi alterado no repositório.

Patch 1/N: <título>
ANTES / DEPOIS com path:linha
```

Patches no estilo do projecto: C# handlers, Result/Problem Details, React/axios patterns.

---

## 7. Falsos positivos descartados

```
🧹 FALSOS POSITIVOS DESCARTADOS

• [AllowAnonymous] em /health/ready
  Motivo: probe Render por design.

• ProtectedRoute sem auth server-side no ficheiro
  Motivo: API exige JWT; UI é camada UX.
```

---

## 8. Cobertura e próximos passos

```
📋 COBERTURA
  Backend:  controllers, handlers, billing, tenant, jobs
  Frontend: auth context, axios, rotas, VITE_*
  Ferramentas: estática, npm audit, dotnet vulnerable, git ls-files

⚡ PRÓXIMOS PASSOS
  1. CRITICAL imediato
  2. HIGH no sprint actual
  3. MEDIUM/LOW backlog
  4. Opcional: DAST, pentest, SAST CI

💡 Limitação: auditoria estática — não substitui testes cross-tenant automatizados nem pentest.
```

---

## Guia de confiança

| Confiança | Quando |
|-----------|--------|
| HIGH | Exploit claro; sem mitigação no caminho |
| MEDIUM | Provável; depende de deploy ou caller não visto |
| LOW | Padrão suspeito — preferir descartar ou INFO |

---

## Ordem das secções

1. Cabeçalho  
2. Resumo executivo  
3. Achados por categoria  
4. Dependências  
5. Segredos  
6. Billing/Stripe (full audit)  
7. Patches (CRITICAL/HIGH)  
8. Falsos positivos descartados  
9. Cobertura e próximos passos
