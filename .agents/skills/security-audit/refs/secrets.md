# Padrões de segredos — PT Manager (.NET + React/Vite)

Usar no **Step 3 (Secrets & Exposure Scan)**. Combinar busca textual + julgamento (evitar alarme em placeholders).

Respeitar ficheiros protegidos de `AGENTS.md` — **nunca ler** `.env`, `*.pem`, `**/secrets/**` em runtime; auditar apenas presença em Git e padrões no código tracked.

---

## Comandos úteis

```bash
# Frontend — dependências vulneráveis
cd frontend && npm audit --omit=dev

# Backend — pacotes vulneráveis
cd backend && dotnet list package --vulnerable --include-transitive

# Ficheiros sensíveis tracked (PowerShell)
git ls-files | Select-String -Pattern '\.(env|pem|key|pfx|p12)$|credentials|appsettings\.Production'

# Linux/macOS
git ls-files | grep -E '\.(env|pem|key|pfx|p12)$|credentials|appsettings\.Production'
```

---

## Prefixos de alto risco (reportar se literal no código tracked)

| Prefixo / padrão | Serviço |
|------------------|---------|
| `sk_live_`, `rk_live_` | Stripe (secreto) |
| `sk_test_` em repo público | Stripe test (MEDIUM em privado → INFO) |
| `whsec_` | Stripe webhook secret |
| `pk_live_` em ficheiro **sem** ser env publishable documentado | Possível misconfig |
| `AKIA[0-9A-Z]{16}` | AWS access key |
| `-----BEGIN (RSA \|EC \|OPENSSH )?PRIVATE KEY-----` | Chave privada |
| `ghp_`, `gho_`, `github_pat_` | GitHub |
| `postgresql://[^:]+:[^@]+@` | Postgres com password na URL |
| Connection string Neon com password em literal | **CRITICAL** |

---

## Variáveis de ambiente esperadas (nunca hardcoded)

### Backend (Render / User Secrets)

- `ConnectionStrings__*` / `DATABASE_URL`
- `JWT` / Identity signing keys
- `Stripe__SecretKey`, `Stripe__WebhookSecret`
- Redis, QStash, Resend, Cloudinary secrets
- Chaves de encriptação

### Frontend (Vercel — só `VITE_*` públicos)

- `VITE_API_URL` — OK
- `VITE_STRIPE_PUBLISHABLE_KEY` — OK (publishable)
- **Nunca** secrets Stripe, JWT secret, connection strings em `VITE_*`

---

## Ficheiros que nunca devem ser commitados

Confirmar no `.gitignore` **e** que não aparecem em `git ls-files`:

```
.env
.env.*
*.pem
*.key
*.p12
*.pfx
**/secrets/**
**/credentials/**
appsettings.Production.json   # se contiver secrets reais
appsettings.Local.json
```

Padrão ausente no `.gitignore` → **MEDIUM** (risco de commit futuro).

Ficheiros protegidos explícitos em `AGENTS.md` — referenciar no relatório.

---

## Onde procurar além de `src/`

- `.github/workflows/` — `${{ secrets.* }}`, não literais
- `render.yaml`, Docker, scripts de deploy
- `appsettings*.json` — placeholders vs valores reais
- `frontend/.env*` — só templates (`.env.example`) tracked
- Comentários, seeds, fixtures de teste
- Histórico Git: mencionar se literal encontrado (`git log -S 'sk_live_' --all`)

---

## Falsos positivos em secrets

- `Configuration["Stripe:SecretKey"]` ou `builder.Configuration[...]` sem fallback literal
- `import.meta.env.VITE_*` sem valor default secreto no código
- Placeholders: `your-api-key`, `changeme`, `xxx`, exemplos em README
- `sk_test_...` em `.env.example` claramente fake
- Hashes em seeds de dev documentados
- Tokens em test doubles (`mock`, `fake`, `test-token`)
- User Secrets ID em `.csproj` sem valores

---

## Acções no relatório quando achar segredo real

1. Rotacionar no painel do provedor **imediatamente**
2. Remover do código; usar env / User Secrets / Render/Vercel secrets
3. Confirmar `.gitignore`
4. Auditar histórico se já houve push
