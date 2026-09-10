# Checklist de vulnerabilidades — React / Vite / PT Manager Frontend

Referência para **Step 4** e **Step 5**. Versões: **`refs/project-stack.md`**.

**Nota:** PT Manager usa **Vite SPA**, não Next.js. Auth real está no backend ASP.NET Core.

---

## 1. Mapa de superfície (Step 1)

| Tipo | Glob / padrão |
|------|----------------|
| HTTP client | `frontend/src/api/**/*.js` |
| Auth | `frontend/src/context/AuthContext.jsx` |
| Axios config | `frontend/src/api/axiosConfig.js` |
| Rotas protegidas | `frontend/src/components/ProtectedRoute.jsx` |
| Páginas | `frontend/src/pages/**` |
| Env exposto | `import.meta.env.VITE_*` |
| Build config | `frontend/vite.config.ts` |

**Inventário dinâmico:** listar deps relevantes (`react`, `vite`, `axios`, `@sentry/react`) com versão do lockfile.

---

## 2. Autenticação e sessão

- [ ] Access token JWT **em memória** (React state/context) — não localStorage/sessionStorage
- [ ] Refresh token **não** acessível a JavaScript (cookie HttpOnly gerido pelo backend)
- [ ] Interceptor axios anexa Bearer token; trata 401/refresh sem expor refresh ao JS
- [ ] Logout limpa estado em memória
- [ ] `ProtectedRoute` redireciona UX — **não** substitui auth no servidor
- [ ] Convites / first-login: tokens de convite não persistidos indevidamente

### Falsos positivos

- `ProtectedRoute` sem validar JWT server-side → **não** reportar como bypass se API exige auth
- Testes com `localStorage` mock → excluir (`*.test.jsx`, `test/setup.js`)

---

## 3. XSS e renderização

- [ ] `dangerouslySetInnerHTML` — ver `refs/false-positives.md` (ex.: shadcn `chart.tsx` com CSS interno)
- [ ] Conteúdo rich text de API: React escapa `{user.bio}` por defeito
- [ ] URLs em `href` de input utilizador: validar protocolo (`javascript:`, `data:`)
- [ ] `target="_blank"` externo: `rel="noopener noreferrer"` (LOW se ausente)

---

## 4. Segredos e config cliente

- [ ] `VITE_*` só com valores **destinados** ao browser (publishable keys, URLs públicas)
- [ ] Nunca `VITE_*` com secret Stripe, JWT secret, connection strings
- [ ] Sentry DSN em frontend é aceitável (público por design) — não confundir com secret API

---

## 5. Chamadas API e autorização

- [ ] Endpoints sensíveis chamados com credenciais; não confiar em UI para esconder acções
- [ ] IDs na URL/body não substituem ownership — backend deve validar (reportar no backend se só UI)
- [ ] Erros axios: não expor stack ou detalhes internos ao utilizador final
- [ ] CORS é responsabilidade do backend — reportar misconfig no backend, não no Vite dev server

---

## 6. Dependências e build

- [ ] `npm audit --omit=dev` sem HIGH/CRITICAL ignorados
- [ ] Sem scripts postinstall suspeitos no `package.json`
- [ ] CDN imports sem SRI (se existirem) → LOW/INFO

---

## 7. Fluxo de dados (Step 5)

```
UI event → axios (Bearer from memory)
  → backend API (auth + tenant)
  → resposta JSON → state React → render
```

Vulnerabilidades típicas: token em localStorage (XSS → roubo de sessão); UI que envia `trainer_id` no body confiando que backend ignora (verificar backend).

---

## 8. Comandos pós-scan sugeridos

```bash
cd frontend
npm audit --omit=dev
npm run lint
npm run test -- --run
npm run build
```
