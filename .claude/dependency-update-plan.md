# Plano de Atualização de Dependências — Julho de 2026

Estado: **Fases 1–2 executadas** (12 jul 2026) — apenas manifests de dependências
atualizados. Validação (`pytest`, lint, build) e refactor do backend ficam para
depois. Fase 3 (majors) não iniciada.

## Pré-requisitos

- Criar branch dedicada.
- Confirmar Node.js 22.13+ (compatível com Vite 8 e ESLint 10) e Python 3.12.
- Guardar os resultados atuais de `pytest`, Vitest, ESLint, Ruff e build.
- Não usar `npm audit fix --force`; atualizar dependências explicitamente.
- Decidir se `requirements.txt` será a fonte de verdade e gerar um lock Python
  separado, ou se o projeto passará a instalar a partir de `pyproject.toml`.

## Fase 1 — Atualizações seguras do backend

Objetivo: atualizar patches/minors compatíveis e sincronizar as duas fontes.

Versões prioritárias:

- FastAPI 0.139.0
- Uvicorn 0.51.0
- SQLModel 0.0.39
- Pydantic 2.13.4 (sem alteração)
- Pydantic Settings 2.14.2
- SQLAlchemy 2.0.51, com limite `<2.1`
- Stripe 15.3.0
- Sentry SDK 2.63.0
- pytest 9.1.1
- Ruff 0.15.21

Passos:

1. Corrigir a descrição para PostgreSQL multi-tenant e alinhar Python/Ruff em
   3.12.
2. Sincronizar extras e dependências em `requirements.txt` e `pyproject.toml`:
   `uvicorn[standard]`, `sentry-sdk[fastapi]`, Jinja2, SlowAPI e SQLAlchemy.
3. Atualizar primeiro FastAPI + SQLModel + Pydantic + SQLAlchemy num único lote
   compatível.
4. Atualizar Uvicorn, Pydantic Settings, Sentry, pytest e Ruff.
5. Atualizar Stripe por último e testar fluxos de billing/webhook.
6. Auditar o ambiente Python com Snyk autenticado ou `pip-audit`.

Comandos, depois de editar os pins:

```powershell
cd backend
python -m pip install --upgrade -r requirements.txt
python -m pip check
ruff check app tests
ruff format --check app tests
pytest
```

Para validar versões antes da instalação:

```powershell
python -m pip index versions fastapi
python -m pip index versions sqlmodel
python -m pip index versions sqlalchemy
```

## Fase 2 — Atualizações seguras do frontend

Prioridade de segurança imediata:

```powershell
cd frontend
npm install axios@1.18.1 react-router-dom@7.18.1
npm install react@19.2.7 react-dom@19.2.7
npm install @chakra-ui/react@3.36.0 tailwindcss@4.3.2 @tailwindcss/vite@4.3.2
npm install -D postcss@8.5.17 vite@7.3.6 vitest@4.1.10
```

Depois, atualizar os restantes packages dentro da major já usada:

```powershell
npm update
npm outdated
npm audit
npm run lint
npm run test -- --run
npm run build
```

Regras da fase:

- Manter React 19; não existe benefício em procurar uma major inexistente.
- Atualizar pares em conjunto: React/React DOM, Tailwind/plugin Vite e
  ESLint/`@eslint/js`.
- Fazer smoke visual de Chakra, Radix, calendários, gráficos e modais.
- Rever o diff de `package-lock.json`; não aceitar alterações sem explicação.

## Fase 3 — Atualizações major e migração de código

Fazer um ramo/commit por grupo, pela ordem:

### 3.1 Vite 8 + plugin React 6

```powershell
cd frontend
node --version
npm install -D vite@8.1.4 @vitejs/plugin-react@6.0.3
npm run build
npm run test -- --run
```

Rever `vite.config.*`, plugins Rollup/Rolldown, aliases, source maps, proxy de
desenvolvimento e build Vercel.

### 3.2 ESLint 10

O registo devolveu ESLint 10.7.0 e `@eslint/js` 10.0.1. Confirmar novamente os
dist-tags antes de instalar, porque estes dois pacotes podem publicar em
cadências diferentes.

```powershell
npm install -D eslint@10.7.0 @eslint/js@10.0.1
npm install -D eslint-plugin-react-hooks@7.1.1 eslint-plugin-react-refresh@0.5.3
npm run lint
```

O projeto já usa flat config. Rever novos erros de `eslint:recommended`,
comentários `eslint-env`, tracking JSX e compatibilidade dos plugins.

### 3.3 Majors de tooling e UI

Tratar separadamente:

- `@types/node` 25 → 26;
- `@vercel/speed-insights` 1 → 2;
- `globals` 16 → 17;
- `lucide-react` 0.x → 1.x;
- `react-day-picker` 9 → 10;
- TypeScript 5 → 7.

Antes de cada instalação, ler o guia de migração oficial, pesquisar imports e
API usadas, atualizar código e executar testes focados. TypeScript 7 deve ser o
último deste grupo, depois de Vite/plugins declararem suporte.

### 3.4 Migração opcional de React Router

`react-router-dom` 7.18.1 é a versão estável mais recente do pacote atual.
Migrar para `react-router` 8 é uma decisão arquitetural separada: inventariar
loaders/actions, guards, redirects e imports, e só então remover
`react-router-dom`. Não juntar esta migração ao patch de segurança.

## Fase 4 — Verificação

Backend:

```powershell
cd backend
python -m pip check
ruff check app tests
ruff format --check app tests
pytest
```

Frontend:

```powershell
cd frontend
npm ci
npm audit
npm run lint
npm run test -- --run
npm run test:coverage
npm run build
```

Smoke manual:

- autenticação, refresh/logout e rotas protegidas;
- isolamento por `trainer_id`;
- CRUD de clientes, sessões, treino, nutrição e suplementos;
- checkout, portal e webhooks Stripe;
- uploads Cloudinary e emails Resend;
- captura de erros Sentry;
- navegação direta/refresh em rotas no Vercel;
- layout responsivo e componentes Chakra/shadcn/Radix.

Critérios de aprovação:

- zero vulnerabilidades altas/críticas no frontend, ou exceção documentada;
- auditoria Python sem vulnerabilidades altas/críticas;
- nenhuma regressão de testes, lint, cobertura ou build;
- lockfiles e fontes de dependências sincronizados;
- versão Node fixada no repositório e CI/deploy.

## Rollback

- Um commit por lote permite reverter apenas a fase problemática.
- Nunca regenerar migrations SQL durante este trabalho.
- Se uma major falhar, manter a última versão segura da major anterior e abrir
  tarefa específica com o erro, reprodução e guia de migração relevante.
