# Stack e Dependências do Frontend

*2026-09-15 — lista aprovada em princípio pelo utilizador, sujeita à auditoria abaixo.*

## ⚠️ Ressalva obrigatória: auditoria de dependências

Nenhum package entra no `package.json` sem passar por esta verificação na Fase 6C (e em
qualquer adição futura). Resultado registado no blueprint da fase.

| Verificação | Critério de aceitação |
|---|---|
| Vulnerabilidades | `npm audit` sem `high`/`critical` não mitigados |
| Versão | Versão exata fixada e registada no blueprint; `package-lock.json` versionado; CI com `npm ci` |
| Manutenção | Release nos últimos 12 meses e issues ativas respondidas |
| Licença | MIT, Apache-2.0, ISC ou BSD |
| Tamanho | Impacto no bundle avaliado (import tree-shakeable, sem dependências transitivas pesadas) |
| Proveniência | Package oficial do autor/organização; nome confirmado (evitar typosquatting) |
| Necessidade | Existe consumidor concreto na fase; sem instalação "para mais tarde" |

As versões **não** são fixadas neste documento; a 6C escolhe a versão estável corrente
no momento da auditoria. Reavaliação de necessidades adicionais no fecho da 6C.

## Base

| Package | Papel |
|---|---|
| `react`, `react-dom` 19 | UI |
| `vite` + `@vitejs/plugin-react` | Build e dev server (HTTPS local, ver arquitetura §5) |
| `typescript` (`strict: true`) | Tipagem em todo o código |
| `tailwindcss` v4 + `@tailwindcss/vite` | Estilos com tokens em CSS variables |
| shadcn/ui (CLI) + `radix-ui` + `class-variance-authority` + `clsx` + `tailwind-merge` | Componentes próprios no repositório |
| `lucide-react` | Ícones |
| `react-router` 7 | Routing com lazy routes |

## Dados e contrato

| Package | Papel | Porquê |
|---|---|---|
| `@tanstack/react-query` | Cache, loading/erro, invalidação, retry | Substitui hooks manuais; padrão de facto |
| `openapi-typescript` (dev) | Gera `schema.d.ts` a partir do OpenAPI do backend | Tipos sempre alinhados com o contrato v1 |
| `openapi-fetch` | Cliente HTTP tipado sobre `fetch` | Muito leve; middleware para Bearer, CSRF e refresh |
| `nuqs` | Estado no URL (paginação, filtros, tabs) | Links partilháveis, botão "voltar" funciona |

## Formulários e tabelas

| Package | Papel |
|---|---|
| `react-hook-form` | Formulários performantes |
| `zod` + `@hookform/resolvers` | Validação tipada e partilhada entre form e parsing |
| `@tanstack/react-table` | Tabelas com paginação/ordenação no servidor (DataTable partilhada) |

## UX

| Package | Papel |
|---|---|
| `sonner` | Toasts (padrão shadcn) |
| `cmdk` | Command palette ⌘K e base do Combobox |
| `motion` | Transições e micro-interações (respeitando `prefers-reduced-motion`) |
| `date-fns` | Datas e fusos (timezone do trainer vem do backend) |
| `recharts` (via shadcn `chart`) | Gráficos de progresso — só quando houver ecrã consumidor |

## Testes e qualidade

| Package | Papel | Fase |
|---|---|---|
| `vitest` + `@testing-library/react` + `@testing-library/user-event` + `jsdom` | Unitários e componentes | 6C |
| `msw` | API simulada em testes e, opcionalmente, em dev sem backend | 6C |
| `eslint` + `typescript-eslint` + `eslint-plugin-react-hooks` | Lint | 6C |
| `prettier` + `prettier-plugin-tailwindcss` | Formatação | 6C |
| `@playwright/test` | E2E | Sprint 8 |

## Excluídos (e porquê)

| Package | Motivo |
|---|---|
| Chakra UI | Segundo sistema de estilos a competir com Tailwind; shadcn já cobre |
| Axios | `openapi-fetch` cobre o caso com tipos gerados |
| Zustand / Redux | Sem estado global que não seja do servidor (Query) ou do URL (nuqs) — reavaliar só com caso concreto |
| `react-toastify` | Substituído por `sonner` |
| Moment.js | Obsoleto e pesado; `date-fns` |
| Bibliotecas de i18n | App só em PT-PT no MVP; reavaliar pós-MVP |

## Assets de marca

Fornecidos pelo utilizador (criados com o Codex), colocados em `frontend/public/`:
`logo.svg` (uso na app, sem perda de qualidade), `favicon.svg`, `favicon.ico`,
`apple-touch-icon.png`. Até chegarem, a 6C usa placeholders com os mesmos nomes.
