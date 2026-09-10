# Headers de segurança (CSP, HSTS) — Render + Vercel

Carregar no **Step 4** ao revisar headers ausentes ou misconfig CORS.

**Regra de ouro:** ausência de CSP custom no repositório **não é vulnerabilidade** por si só. No máximo **INFO**.

PT Manager: API em **Render**, SPA em **Vercel** — headers podem estar na plataforma, não no repo.

---

## 1. O que verificar ANTES de sugerir headers

| Ordem | Onde olhar |
| ----- | ---------- |
| 1 | `backend/src/Api/Program.cs` e extensions de middleware |
| 2 | `render.yaml` ou config Render (se no repo) |
| 3 | `frontend/vercel.json` (se existir) |
| 4 | Painéis Render/Vercel (mencionar no relatório se não auditável estaticamente) |
| 5 | CORS na API — allowlist vs `AllowAnyOrigin` + credenciais |

Nunca colar CSP genérica que quebre Stripe.js, Sentry, ou API cross-origin Vercel→Render.

---

## 2. Severidade permitida

| Achado | Severidade máxima | Confiança |
| ------ | ----------------- | --------- |
| Sem CSP custom no repo (API ou SPA) | **INFO** | LOW |
| Sem HSTS no repo | **INFO** — Vercel/Render aplicam na borda | LOW |
| `X-Frame-Options` / `frame-ancestors` ausente | **LOW** | LOW |
| CORS `AllowAnyOrigin` + cookies credenciais na API | **HIGH** | HIGH |
| CSP permissiva + XSS stored confirmado | **MEDIUM** | MEDIUM |

**Proibido:** CRITICAL/HIGH **somente** por “não tem CSP no vercel.json”.

---

## 3. HSTS

- Vercel e Render aplicam HTTPS e HSTS na borda em produção
- Ausência no código fonte é **normal**
- Relatório: _“Confirmar no painel Render/Vercel”_

---

## 4. CSP e SPA Vite

CSP estrita pode quebrar:

- Inline scripts do bundle Vite (dev vs prod)
- Tailwind / estilos inline
- Sentry, Speed Insights, Stripe.js (se loaded no cliente)
- Imagens Cloudinary, avatares externos

**Ao sugerir CSP (só se pedido ou INFO detalhado):**

1. Inventariar domínios usados (grep `https://`, env `VITE_*`)
2. Rollout gradual (report-only → enforce)
3. Testar `npm run build && npm run preview`

Alternativa: CSP no **Vercel** dashboard, não hardcoded no repo.

---

## 5. API ASP.NET Core

Headers úteis (se não na plataforma):

- `X-Content-Type-Options: nosniff`
- `Referrer-Policy`
- Remover `Server` banner se exposto

Não propor middleware de headers sem verificar duplicação com Render.

---

## 6. Texto modelo para relatório (INFO)

```
⚪ INFO — Headers de segurança custom

  API (Render) e SPA (Vercel) — sem CSP/HSTS definidos no repositório.
  Plataformas podem aplicar HTTPS/HSTS na borda — não verificável só pelo repo.
  CSP custom exige allowlist por integrações (Sentry, Stripe.js, API cross-origin).

  Próximo passo (opcional): política no painel Vercel/Render ou CSP report-only.
  Confiança: LOW
```

---

## 7. Quando ESCALAR além de INFO

- XSS confirmado + CSP ausente ou `unsafe-inline` em páginas autenticadas
- API com refresh cookie + CORS `*` + credentials
- Problem Details ou headers que vazam versão/stack em rotas sensíveis (LOW/MEDIUM)

---

## 8. Integração

Entradas relacionadas em `refs/false-positives.md` — aplicar antes de publicar achado.
