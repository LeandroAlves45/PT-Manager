# Referência — frontend TS/TSX

Complementa `SKILL.md`. Ler quando o ficheiro documentado é `frontend/src/**`. A origem é
o desenho aprovado em `.claude/project/frontend/` e `layout/`.

## Camada e responsabilidade

`app/`, `features/<nome>/` ou `shared/`, e porquê essa e não outra.

## O bloco de código inclui, quando aplicável

1. Só os `import` necessários, com alias `@/` (nunca caminhos relativos que atravessem
   camadas).
2. JSDoc em português nos limites públicos: módulos de `shared/`, hooks públicos de
   features, componentes exportados, e qualquer função cuja intenção não seja óbvia pelo
   nome.
3. Tipos explícitos nos limites; `any` proibido — usar `unknown` e estreitar.
4. Tipos da API importados de `@/shared/api/schema` (gerado). Nunca escrever à mão um tipo
   de resposta que já exista no `schema.d.ts`.
5. `AbortSignal` propagado onde o TanStack Query o fornece.
6. Estados obrigatórios em todo o ecrã de dados: loading (skeleton), vazio e erro.
7. Comentários só quando explicam o motivo de uma decisão não evidente — em português.

## Idiomas

| Elemento | Idioma |
|---|---|
| Identificadores, nomes de ficheiros e pastas, commits | Inglês |
| `describe` / `it` / `test` nos testes | Inglês |
| Texto visível ao utilizador (labels, mensagens, `aria-label`) | PT-PT |
| Comentários e JSDoc | PT-PT |
| Documento `.md` do blueprint | PT-PT |

## Regras arquitecturais (de `00_ARQUITETURA_FRONTEND.md`)

1. Dependências numa só direcção: `app → features → shared`. `shared` nunca importa de
   `features`.
2. Uma feature nunca importa ficheiros internos de outra; só o `index.ts` público.
3. Acesso HTTP exclusivamente por `shared/api/client.ts` (openapi-fetch). Sem `fetch` nem
   `axios` em componentes, páginas ou hooks de feature.
4. Estado do servidor no TanStack Query; filtros e paginação no URL com `nuqs`; estado local
   em `useState`. Sem store global sem caso concreto.
5. Access token e CSRF token só em memória. Nada sensível em `localStorage`/`sessionStorage`.
6. Campos da API mantêm-se em snake_case; a tradução para PT-PT faz-se em mapas de labels.
7. Cores apenas por tokens semânticos; nenhum hex em JSX.
8. Rotas e componentes condicionados por papel (`superuser`/`trainer`/`client`) usam sempre
   os guards definidos em `app/router`, nunca uma verificação de papel ad-hoc dentro de um
   componente de feature.

## Orçamento de pedidos (equivalente ao budget de queries EF do backend)

Por caso de uso, declarar:

1. Quantos pedidos HTTP são feitos ao montar o ecrã e quais.
2. A query key de cada um e o que a invalida.
3. Paginação (`page_number`/`page_size`) e ordenação usadas.
4. O que fica em `placeholderData: keepPreviousData` e porquê.

Um ecrã que dispare um pedido por linha de uma lista é um defeito, não uma escolha.

## Validação obrigatória específica de frontend

Além da lista comum em `SKILL.md`:

1. `npm ci`, `lint`, `typecheck`, `test -- --run` e `build` verdes na materialização.
2. `schema.d.ts` gerado por `openapi-typescript` contra o backend real é **idêntico** ao do
   blueprint.
3. Os testes provam comportamento visível (Testing Library por role/label), não
   implementação.
4. Greps de proibição sem resultados: `localStorage`/`sessionStorage` com tokens, `axios`,
   `fetch(` fora de `shared/api/client.ts`, `#` hexadecimal em `.tsx`, `any`.
5. Um ecrã ou rota condicionada por papel tem pelo menos um teste negativo: o papel sem
   acesso não vê o conteúdo e, ao navegar directamente para a rota, o guard redirecciona. A
   mesma lógica que o backend aplica a campos de DTO por actor aplica-se aqui a rotas e
   componentes — sem este teste, um erro de condição no JSX ou no guard fica invisível até
   alguém testar com a sessão errada.
6. Quando um ficheiro do lote depende de algo criado noutro sub-lote (um hook partilhado, um
   tipo gerado do schema, um provider), declarar essa dependência e a ordem de aplicação na
   nota de mentor, tal como no backend — a divisão em sub-lotes (ex.: 6C-1 a 6C-4) torna este
   tipo de dependência tão comum no frontend como no backend.
7. `git status` no repositório real prova que nenhum ficheiro de `frontend/` ou `backend/`
   mudou.

## Exemplo real

Excerto do blueprint `docs/blueprints/frontend-files/sprint_6/sprint_6C/05_utilitarios_configuracao_e_hooks_partilhados.md`,
validado e fechado nessa fase. Mostra o formato esperado: caminho, estado, JSDoc em
português a explicar a origem de um valor que não é óbvio pelo código, e a nota de mentor a
seguir ao bloco.

### `frontend/src/shared/hooks/useDebounce.ts`

**Estado:** a criar · **Camada:** shared/hooks · **Linhas:** 21

```ts
import { useEffect, useState } from 'react';

/**
 * Devolve o valor só depois de ele estar estável durante `delayMs`.
 *
 * Usado na pesquisa da command palette e nos filtros de listagem: sem isto, cada tecla
 * premida seria um pedido à API.
 *
 * @param value Valor a atrasar.
 * @param delayMs Tempo de estabilidade exigido; 300 ms é o valor do desenho aprovado.
 */
export function useDebounce<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(timer);
  }, [value, delayMs]);

  return debounced;
}
```

**Nota de mentor.** 300 ms é o valor do desenho aprovado para a pesquisa da paleta de
comandos — não é um número arbitrário escolhido ao escrever o hook.

**Validações.** Consumido por `CommandMenu`.
