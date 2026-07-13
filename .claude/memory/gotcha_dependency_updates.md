# Atualização de Dependências — Julho de 2026

**Estado:** Fases 1–2 executadas (12 jul 2026). Apenas manifests atualizados;
validação e refactor do backend adiados.

Pesquisa efetuada em 12 de julho de 2026, usando PyPI, npm, changelogs oficiais,
GitHub Advisories e `npm outdated`/`npm audit`. Não foram instaladas dependências.

## Âmbito e estado dos lockfiles

- Backend: 26 dependências diretas únicas em `requirements.txt`; não existe lockfile
  Python. `pyproject.toml` repete intervalos mais permissivos e não é uma fonte
  reprodutível equivalente.
- Frontend: 59 dependências diretas (36 runtime + 23 desenvolvimento).
  `package-lock.json` existe, usa lockfile v3 e fixa 651 pacotes no total.
- Total analisado: 85 declarações diretas únicas.
- Classificação para planeamento: 75 atualizações seguras/no-op dentro da mesma
  linha principal e 10 atualizações major que exigem trabalho isolado.

## Dependências prioritárias

| Dependência | Declarado | Instalado/efetivo | Última estável | Risco | Ação |
| --- | --- | --- | --- | --- | --- |
| fastapi | `0.136.1` | `0.136.1` | `0.139.0` | Médio | Atualizar e validar OpenAPI, lifespan e respostas |
| uvicorn | `0.46.0` | `0.46.0` | `0.51.0` | Médio | Atualizar com extra `[standard]` consistente |
| sqlmodel | `0.0.38` | `0.0.38` | `0.0.39` | Baixo | Atualização de correção |
| pydantic | `2.13.4` | `2.13.4` | `2.13.4` | Baixo | Já atual |
| pydantic-settings | `2.14.1` | `2.14.1` | `2.14.2` | Baixo | Atualização de correção |
| sqlalchemy | transitiva, sem pin | desconhecido | `2.0.51` | Médio | Declarar `>=2.0.51,<2.1`; SQLModel ainda exclui 2.1 |
| stripe | `15.1.0` | `15.1.0` | `15.3.0` | Médio | Rever versão API Stripe fixada pelo SDK |
| sentry-sdk | `2.59.0` | `2.59.0` | `2.63.0` | Baixo | Atualizar e testar integração FastAPI |
| pytest | `9.0.3` | `9.0.3` | `9.1.1` | Baixo | Atualizar plugins em conjunto |
| ruff | `0.15.12` | `0.15.12` | `0.15.21` | Baixo | Atualizar e rever novos diagnósticos |
| react | `^19.2.0` | `19.2.4` | `19.2.7` | Baixo | Manter React 19; não há major para fazer |
| react-dom | `^19.2.0` | `19.2.4` | `19.2.7` | Baixo | Atualizar em conjunto com React |
| vite | `^7.3.1` | `7.3.1` | `8.1.4` | Alto | Primeiro corrigir para 7.3.6; migrar Vite 8 isoladamente |
| tailwindcss | `^4.1.18` | `4.1.18` | `4.3.2` | Médio | Atualizar com `@tailwindcss/vite` na mesma versão |
| @chakra-ui/react | `^3.33.0` | `3.33.0` | `3.36.0` | Baixo | Atualização minor; smoke visual obrigatório |
| axios | `^1.13.5` | `1.13.5` | `1.18.1` | Alto (segurança) | Atualizar imediatamente para 1.18.1 |
| react-router-dom | `^7.13.0` | `7.13.0` | `7.18.1` | Alto (segurança) | Atualizar para 7.18.1 e testar autorização/navegação |
| vitest | `^4.1.1` | `4.1.1` | `4.1.10` | Baixo | Atualização patch |
| eslint | `^9.39.1` | `9.39.2` | `10.7.0` | Alto | Major isolada; atualizar também `@eslint/js` |

Notas:

- Os valores npm acima usam o registo consultado por `npm outdated` em 12-07-2026;
  são mais recentes do que alguns resultados indexados pela pesquisa web.
- `react-router-dom` 7.18.1 continua a ser a versão estável mais recente desse
  pacote; React Router 8 existe no pacote `react-router`, mas trocar de pacote é
  uma migração separada, não uma simples atualização.
- O lockfile diverge do manifesto em alguns pins permitidos por `^`, por exemplo
  React 19.2.4 e ESLint 9.39.2.

## Atualizações frontend fora da tabela prioritária

`npm outdated` encontrou atualizações dentro da gama atual para Radix UI,
`@sentry/react` 10.65.0, `@types/react` 19.2.17, Autoprefixer 10.5.2,
Framer Motion 12.42.2, jsdom 29.1.1, MSW 2.15.0, PostCSS 8.5.17,
Prettier 3.9.5, React Hook Form 7.81.0, Recharts 3.9.2 e
Tailwind Merge 3.6.0. Devem entrar na fase segura, em lotes pequenos.

As 10 linhas major a isolar são:

1. `@eslint/js` 9 → 10;
2. `@types/node` 25 → 26;
3. `@vercel/speed-insights` 1 → 2;
4. `@vitejs/plugin-react` 5 → 6;
5. `eslint` 9 → 10;
6. `globals` 16 → 17;
7. `lucide-react` 0.x → 1.x;
8. `react-day-picker` 9 → 10;
9. `typescript` 5 → 7;
10. `vite` 7 → 8.

`eslint-plugin-react-refresh` 0.4 → 0.5 também deve ser tratado com cautela por
estar antes de 1.0, embora não seja contado como major SemVer.

## Segurança

O `npm audit` do lockfile atual encontrou 18 vulnerabilidades: 10 altas,
6 moderadas, 2 baixas e 0 críticas. Quatro dependências diretas estão envolvidas:

- Axios 1.13.5: várias falhas, incluindo SSRF/NO_PROXY e manipulação por
  prototype pollution; as correções acumuladas exigem pelo menos 1.15.2.
  Recomenda-se diretamente 1.18.1.
- React Router DOM 7.13.0: herda vulnerabilidades altas de `react-router`;
  atualizar para 7.18.1.
- Vite 7.3.1: múltiplos avisos de divulgação/path traversal. A
  CVE-2026-39364 (CVSS 8.2) é corrigida em 7.3.2; outros avisos Windows exigem
  uma versão 7.3.x posterior. Usar 7.3.6 antes da migração para 8.1.4.
- PostCSS 8.5.6: XSS na serialização CSS, corrigido em 8.5.10; atualizar para
  8.5.17.

Existem ainda falhas transitivas em Babel, AJV, picomatch, Rollup, Undici, YAML
e outras. Regenerar o lockfile após os bumps e exigir `npm audit` sem
vulnerabilidades altas.

O Snyk não pôde complementar a análise porque a autenticação falhou. Antes da
implementação, executar uma auditoria Python autenticada (`snyk test` ou
`pip-audit`) para cobrir o backend, que não tem lockfile.

## Incompatibilidades e gotchas

### Vite 8

- Requer Node.js 20.19+ ou 22.12+.
- Adota Rolldown como bundler; opções e plugins Rollup personalizados podem
  precisar de adaptação (`build.rollupOptions` → `build.rolldownOptions`).
- Atualizar `@vitejs/plugin-react` 6 no mesmo ramo.
- Confirmar build Vercel e source maps Sentry.

### ESLint 10

- Requer Node.js 20.19+, 22.13+ ou 24+.
- Remove definitivamente `.eslintrc`; o projeto já usa `eslint.config.js`,
  reduzindo o risco.
- Altera `eslint:recommended`, pesquisa de configuração, tracking JSX e APIs
  de regras. Atualizar plugins e resolver novos erros reais, sem os desligar.

### Stripe 15.1 → 15.3

- Não é uma mudança major do SDK, mas muda a versão API predefinida de
  `2026-04-22.dahlia` para `2026-06-24.dahlia`.
- Testar criação de checkout, portal, subscrições e webhooks com fixtures.
- As quebras maiores do Stripe 15 (`StripeObject` já não herda de `dict` e
  decimais usam `Decimal`) já deveriam estar absorvidas pelo pin 15.1, mas
  convém procurar `.get()`, `.items()` e `to_dict_recursive()`.

### SQLModel/SQLAlchemy

- SQLModel 0.0.39 requer SQLAlchemy `>=2.0.14,<2.1.0`.
- Não instalar SQLAlchemy 2.1 beta. Fixar a linha 2.0 para builds reproduzíveis.

## Inconsistências de metadados

- `pyproject.toml` descreve uma API “Single-user” com SQLite; o produto é
  multi-tenant e usa PostgreSQL.
- `requires-python = ">=3.11"` e Ruff `target-version = "py311"` divergem da
  stack declarada Python 3.12.
- `requirements.txt` fixa versões exatas, enquanto `pyproject.toml` usa mínimos
  muito antigos. É necessário escolher uma fonte de verdade e sincronizar.
- `jinja2` existe apenas em `requirements.txt`; SQLAlchemy não está declarada
  diretamente; `slowapi` não tem pin exato; `uvicorn[standard]` só aparece no
  `pyproject.toml`.

## Critérios de conclusão

**Fases 1–2 (manifests):** concluídas — `requirements.txt`, `pyproject.toml`,
`package.json` e `package-lock.json` atualizados.

**Pendente (após refactor backend / Clean Architecture):**

- Backend e frontend instalam do zero de forma reproduzível.
- `pytest`, Ruff, Vitest, ESLint e build Vite passam.
- `npm audit` não apresenta vulnerabilidades altas ou críticas.
- Smoke tests cobrem login, multi-tenancy, rotas protegidas, checkout Stripe,
  webhooks, upload Cloudinary e captura Sentry.

### Nota pós-bump (resolver na refactor)

- `passlib` 1.7.4 + `bcrypt` 5.0.0 são incompatíveis em runtime (hash de passwords
  falha). Na refactor, migrar `app/core/security.py` para `bcrypt` nativo ou
  fixar `bcrypt<5` até remover `passlib`.

### Patches de segurança (12 jul 2026)

| Pacote | Versão anterior | Versão corrigida | CVEs / GHSAs |
| --- | --- | --- | --- |
| python-multipart | 0.0.28 | **0.0.32** | GHSA-5rvq-cxj2-64vf, GHSA-6jv3-5f52-599m, GHSA-v9pg-7xvm-68hf, GHSA-vffw-93wf-4j4q |
| PyJWT | 2.12.1 | **2.13.0** | GHSA-993g-76c3-p5m4, GHSA-fhv5-28vv-h8m8, GHSA-jq35-7prp-9v3f, GHSA-w7vc-732c-9m39, GHSA-xgmm-8j9v-c9wx, PYSEC-2026-175/177/178/179 |
| cryptography | 48.0.0 | **48.0.1** | GHSA-537c-gmf6-5ccf (OpenSSL nas wheels) |

**Falsos positivos VersionLens (já corrigidos na versão instalada):**

- `uvicorn[standard]==0.51.0` — CVEs de 2020 corrigidos em ≥0.11.7; o alerta vem
  do extra `[standard]` não ser parseado (`uvicorn@:`).
- `sentry-sdk==2.63.0` — GHSA-g92j / PYSEC-2026-1917 corrigidos em ≥2.8.0;
  GHSA-29pr-6jr8-q5jm é risco de **config** (`sendDefaultPII=True`), não de versão.
