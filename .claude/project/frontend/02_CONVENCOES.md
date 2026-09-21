# Convenções do Frontend

*2026-09-15*

## 1. TypeScript

- `strict: true`, `noUncheckedIndexedAccess: true`. Proibido `any`; usar `unknown` e estreitar.
- Tipos da API importados de `@/shared/api/schema` (gerado). Alias legíveis por feature:
  `type Client = components['schemas']['ClientResponse']`.
- `satisfies` para mapas de configuração (ex.: variantes de estado).
- Enums do backend chegam em snake_case (`JsonStringEnumConverter` SnakeCaseLower) e
  mantêm-se assim no frontend; a tradução para texto PT-PT é feita num mapa de labels.

## 2. Naming e ficheiros

| Elemento | Convenção | Exemplo |
|---|---|---|
| Componentes | PascalCase, um por ficheiro | `ClientForm.tsx` |
| Páginas | sufixo `Page` | `ClientsPage.tsx` |
| Hooks | `use` + camelCase | `useClientsQuery.ts` |
| Schemas zod | `<recurso>.schema.ts` | `client.schema.ts` |
| Pastas | kebab-case | `trainer-settings/` |
| Imports | absolutos com `@/` | `import { Button } from '@/shared/components/ui/button'` |

- Campos da API mantêm snake_case (`full_name`, `page_size`). Não converter para camelCase:
  evita uma camada de mapeamento e bugs de payload vazio.
- Textos visíveis em PT-PT. Código, nomes de ficheiros e commits em inglês.
- **Comentários e JSDoc em PT-PT** (decisão de 2026-09-20): o código fala inglês, as notas
  explicativas falam a língua de quem as lê.

## 3. Acesso à API

- Só através de `shared/api/client.ts`. Nunca `fetch` em componentes.
- Cada feature expõe hooks em `api/queries.ts` e `api/mutations.ts`.
- Query keys em `api/keys.ts`, hierárquicas:

```ts
/**
 * Query keys da feature clients.
 * Hierarquia permite invalidar tudo (`all`), só listas ou um detalhe.
 */
export const clientKeys = {
  all: ['clients'] as const,
  lists: () => [...clientKeys.all, 'list'] as const,
  list: (filters: ClientListFilters) => [...clientKeys.lists(), filters] as const,
  detail: (clientId: string) => [...clientKeys.all, 'detail', clientId] as const,
};
```

- Paginação: `page_number` (base 1) e `page_size` (default 50 no backend). Resposta
  `PagedResponse { items, total_count, page_number, page_size }`.
- Uploads (`logo`, `avatar`): `multipart/form-data`, máx. 6 MiB; validar tamanho e tipo
  no cliente antes de enviar, mas a autoridade é o backend.

## 4. Erros

O backend responde `application/problem+json`:

```json
{
  "status": 400,
  "title": "validation_failed",
  "detail": "…",
  "instance": "/api/v1/clients",
  "correlation_id": "…",
  "errors": [{ "field": "email", "code": "…", "message": "…" }]
}
```

| Status | Tratamento na UI |
|---|---|
| 400 com `errors[]` | `form.setError(field, { message })` por campo; toast só se não houver campo |
| 401 | Refresh automático (single-flight); se falhar, ir para login |
| 402 | Plano/subscrição insuficiente → CTA para billing |
| 403 | Mensagem "sem permissão"; nunca expor detalhes de outro tenant |
| 404 | Estado vazio "não encontrado" com ação de voltar |
| 409 | Mensagem específica pelo `title` (código de erro) |
| 429 | Toast "demasiados pedidos, tente dentro de momentos"; sem retry automático |
| 503 | Dependência externa indisponível (Stripe, Cloudinary) → mensagem e retry manual |

- `title` é o código estável do erro; mensagens PT-PT vêm de um mapa por código, com
  `detail` como fallback.
- Mostrar `correlation_id` em erros inesperados (copiável) para depuração.
- TanStack Query: sem retry em 4xx; até 2 retries em falhas de rede/5xx de leituras.

## 5. Formulários

- `react-hook-form` + `zod` + componentes `Form*` do shadcn.
- Schema zod espelha as regras do backend conhecidas (ex.: password 8–128), mas o backend
  é a autoridade.
- Botão de submit desativado durante a mutation; feedback com `sonner` no sucesso.

## 6. Componentes

- Primitivas shadcn ficam em `shared/components/ui/` e só se editam para ajustar ao design
  system. Composições reutilizáveis em `shared/components/`.
- Todo o ecrã de dados tem estados **loading (skeleton)**, **vazio** e **erro**.
- Seleção em catálogos com `Combobox` (ver 03 §5), nunca `<select>` nativo para listas longas.
- Cores apenas por tokens semânticos; nada de hex em JSX.

## 7. Acessibilidade

WCAG 2.2 AA: contraste 4.5:1, foco visível, navegação por teclado completa, `aria-label` em
botões só com ícone, estado nunca comunicado só por cor, `prefers-reduced-motion` respeitado.

## 8. Testes

- Ficheiro de teste ao lado do código: `ClientForm.test.tsx`.
- **Nomes de teste em inglês** (decisão de 2026-09-20): `describe('ClientForm')` e
  `it('should show a field error when the server rejects the email')`. Só o texto que a UI
  mostra — e que a asserção procura — está em PT-PT.
- Testar comportamento visível (Testing Library por role/label), não implementação.
- MSW com handlers por feature em `src/test/msw/`; respostas com a mesma forma do OpenAPI.
- Cada feature fecha com: lista, detalhe, criação, erro de validação do servidor e 403.

## 9. Documentação

- JSDoc em módulos de `shared/` e em hooks públicos de features.
- Blueprints com código integral em `docs/blueprints/frontend-files/sprint_6/` seguem a regra
  do projeto: explicação detalhada do funcionamento e JSDoc completo. Formato na skill
  `blueprints-codigo-real`, referência `references/frontend-typescript.md`
  (`.claude/skills/blueprints-codigo-real/references/frontend-typescript.md`).

## 10. Git

- Branch por fase: `sprint-6/6c-fundacoes`, `sprint-6/6d-admin`, `sprint-6/6e-trainer`,
  `sprint-6/6f-portal`, `sprint-6/6g-auth`. (As fases 6A e 6B são backend, não frontend.)
- Commits convencionais: `feat(frontend): …`, `test(frontend): …`, `chore(frontend): …`.
- CI mínimo (6C): `npm ci`, `npm run lint`, `npm run typecheck`, `npm run test:run`, `npm run build`.
