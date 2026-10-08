# Gotchas — Frontend

Padrões de erro identificados depois de correções do utilizador, no frontend (React / TypeScript / Vitest).
Entradas novas: registar de imediato, antes de continuar. O antigo `correction.md` foi migrado para aqui e apagado em 2026-09-29 (histórico no git).

## 2026-09-23 — Testes do frontend vivem em `frontend/src/test/`, não ao lado do código

**O que aconteceu.** No fecho da 6C comecei a criar `session.test.ts` e `problem.test.ts` ao
lado dos ficheiros de produção (`src/shared/api/`), como indica o doc 11. O utilizador corrigiu:
os testes ficam todos em `frontend/src/test/`, sem misturar com o código.

**A regra.** Testes frontend em `frontend/src/test/`, espelhando o caminho do ficheiro testado
(ex.: `src/shared/api/client.ts` → `src/test/shared/api/client.test.ts`). Infra (MSW, setup,
render) também em `src/test/`. Os caminhos do doc 11 ficam registados como desvio aprovado.

## 2026-09-24 — Gestão de vídeo do admin entra na 6D

**O que aconteceu.** O pack 6D (planeado com o Codex) deixava upload e remoção de vídeo pelo
admin para fase futura. No fecho, o utilizador decidiu fechar a página de admin já com essa
feature. O backend 5D já expunha todos os endpoints (`/global-exercises/{id}/video`), por isso
o trabalho é só frontend.

**A regra.** Antes de adiar uma feature para "fase futura", verificar se o backend já a suporta;
se só falta UI, apresentar ao utilizador o custo real antes de a empurrar. Upload com barra de
progresso (XHR) foi a opção escolhida. O teste real com R2 continua dependente de
`QG5D-PROVIDER-001` (credenciais + CORS PUT no bucket).

## 2026-09-25 — Sexo do cliente deixa de vir pré-escolhido (6E-1)

**O que aconteceu.** O blueprint 05 (`ClientForm`) definia `sex: … ?? 'female'` para clientes
novos. Na revisão de fecho viu-se que isso grava "Feminino" sem o trainer escolher e torna
inalcançável a mensagem "Escolhe o sexo biológico.".

**A regra.** Campos que alimentam cálculos (sexo, nível de atividade) não têm valor por omissão
em criação: opção vazia desativada "Escolhe…" e validação Zod. Mutação M17 protege-o.

## 2026-09-29 — Doc de testes por aplicar pela segunda vez partiu o CI (6E-2)

**O que aconteceu.** Os docs 03–06 da 6E-2 foram aplicados no commit `d33d68e` sem o doc 07
(testes), apesar de o doc 01 pedir o mesmo commit. O CI #15 falhou no passo "Test" do Frontend:
o teste existente do painel ainda esperava a mensagem antiga "…presença depois da hora de
início.", que o doc 05 unificou para "…presença ou a falta…". É a mesma falha da 6E-1.

**A regra.** Uma mudança de copy num ficheiro de produção parte os testes que a citam. Antes
de fazer commit de docs de implementação, aplicar o doc de testes da fatia **ou** correr
`npm run test:run` localmente; nunca empurrar só a produção.

## 2026-09-29 — Copy alterada à mão diverge do teste do blueprint

**O que aconteceu.** Na aplicação manual, `client_inactive` passou de "…antes de lhe vender um
pack." para "…antes de lhe atribuir um pack." (e outros retoques de copy). O teste do doc 07
citava a frase original e falhou; foi adaptado à copy nova (desvio aprovado). O botão continua
a chamar-se "Vender pack" — terminologia a uniformizar numa passagem de copy.

**A regra.** Ao retocar copy de um blueprint, procurar a frase em `frontend/src/test/` e no doc
de testes da fatia (`grep -rn "frase"`) e atualizar os dois no mesmo passo.

## 2026-09-29 — Rótulo de estado com ponto final

**O que aconteceu.** `SESSION_STATUS_LABELS.cancelled_by_trainer` ficou "Cancelada por ti." —
o único rótulo de badge com pontuação. Corrigido; teste "shows each final status with its
label" e mutação N1.

**A regra.** Rótulos de badges, estados e opções não levam ponto final; frases de toast e de
erro levam.

## 2026-09-29 — Submeter antes de chegarem os dados de que o formulário depende

**O que aconteceu.** O revisor sonnet apontou (confiança 40) e a leitura do código confirmou:
em "Marcar sessão", com os packs do cliente ainda a carregar, o `<select>` de Pack estava
desativado mas o botão não. Submeter nessa janela marcava a sessão **sem pack** em silêncio
(o valor era `''`), ou, ao trocar de cliente, com o pack do cliente anterior (409). Corrigido:
submit desativado enquanto `chosen !== null && packs.isPending`; teste com o pedido `usable`
retido por uma promessa e mutação N2.

**A regra.** Um formulário cujo valor por omissão vem de uma query dependente (pré-escolha)
não se submete enquanto essa query está pendente.

## 2026-09-29 — Sugestão automática que sobrevive à troca de opção

**O que aconteceu.** Em "Vender pack", escolher um tipo com duração sugeria o fim previsto;
trocar para um tipo **sem** duração deixava a data do tipo anterior, que era enviada. Corrigido
com um `useRef` da última sugestão: um tipo sem duração apaga a sugestão, nunca uma data
escrita à mão. Dois testes (apaga a sugestão / mantém a data manual) e mutação N3.

**A regra.** Um campo preenchido por sugestão guarda a origem do valor; ao mudar a opção que o
gerou, a sugestão sai, o valor manual fica.

## 2026-09-29 — Vitest local: "Timeout waiting for worker to respond" não é falha de teste

**O que aconteceu.** Com o Docker ligado (3,6 GB livres de 16,5), `vitest run` abriu 15 workers
jsdom (16 CPUs) e 15 ficheiros nem chegaram a arrancar: "Failed to start forks worker". Com
`--maxWorkers=4`, 23/23 ficheiros correram. Causa: memória, não o código nem os testes.

**A regra.** Localmente, com Docker ou outra carga, correr `npx vitest run --maxWorkers=4`. Um
erro de arranque de worker não conta como teste verde nem como teste vermelho: repetir com
menos workers e só então ler o resultado. Na mutação, uma "morte" sem nome de teste falhado é
suspeita e repete-se.

## 2026-09-29 — Testes de datas no fuso da máquina escondem o offset

**O que aconteceu.** A mutação N9 (enviar `starts_at` com `Z` em vez do offset local) sobreviveu:
o teste usava 15 de janeiro, e em Lisboa o offset de inverno é 0, igual ao UTC do CI. Nenhuma
máquina conseguia distinguir "offset local" de "UTC".

**A regra.** Testes do frontend correm com `TZ: 'Europe/Lisbon'` fixo em `vite.config.ts`
(`test.env`); testes de instantes com offset usam uma data de **verão** (+01:00) e o valor
literal esperado (`'2099-07-15T12:15:00+01:00'`), não o mesmo `formatISO` que a produção usa.

## 2026-09-29 — Pesquisa com debounce atrasa a limpeza (6E-3, materialização)

**O que aconteceu.** Na biblioteca (`?tab=`), trocar de separador apaga `search` do URL, mas o
valor com `useDebounce` só muda 300 ms depois: o separador novo era pedido com a pesquisa do
anterior (`GET /supplements?search=batido`) e mostrava "Sem resultados" por instantes. O mesmo
em "Limpar filtros". Apanhado pelo teste de URL, que compara os parâmetros do primeiro pedido.

**A regra.** O debounce serve para escrever, não para apagar: `const search = term === '' ? '' :
debounced`. Testes de troca de separador/limpeza verificam os parâmetros do **primeiro** pedido
do ecrã novo, não só o último.

## 2026-09-29 — `role="combobox"` e opções cmdk não tiram o nome do conteúdo

**O que aconteceu.** No `MuscleGroupPicker`, o botão `role="combobox"` com o texto "Escolher grupos
musculares" não tinha nome acessível (o combobox não herda o nome do conteúdo; só do `<label>`), e
a opção escolhida anunciava "Bícepsselecionado" (dois `<span>` colados).

**A regra.** Um combobox leva sempre rótulo por `htmlFor`/`FormField` (ou `aria-labelledby`); numa
opção com estado, dar `aria-label` explícito (`"Bíceps, selecionado"`) e não usar `aria-selected`,
que no cmdk é o item em foco.

## 2026-09-29 — Revisão sonnet da 6E-3: refine sobre NaN e URL assinado em cache

**O que aconteceu.** (1) O `.refine` da soma dos macros usava `Number("abc")` = NaN, `NaN <= 100`
é falso, e o erro da soma escrevia por cima do erro do campo ("abc" na proteína mostrava "não
podem somar mais de 100 g"). (2) Painel de vídeo (vindo da 6D): depois de uma substituição
ficar pronta, "Ver vídeo" reutilizava o URL assinado do vídeo anterior (cache de 10 min).
(3) Com o debounce na página, trocar de separador e escrever logo pedia o separador novo com o
termo antigo; o debounce passou para dentro de cada separador (o Radix desmonta os inativos).

**A regra.** Validações de objeto só correm quando os campos de que dependem são válidos. Um
recurso que muda (vídeo substituído) invalida as caches derivadas dele (URL de reprodução), não
só a lista. Estado com atraso (debounce) vive no componente que desmonta com o contexto.

## 2026-09-30 — Fecho da 6E-3: classe Tailwind inexistente, soma em vírgula flutuante, polling que morre com o painel

**O que aconteceu.** (1) `wrap-break-words` (não existe; o Tailwind 4 tem `wrap-break-word`) passou
lint, typecheck, testes e build: uma classe inválida é ignorada em silêncio. (2) A regra "macros
≤ 100 g" somava `Number`s: 0,15 + 65,01 + 34,84 = 100,00000000000001 e recusava um alimento que o
backend (`decimal`) aceita. (3) A única invalidação da lista depois de um vídeo ficar pronto era
um efeito do `queryFn` do polling do painel; fechar o painel (o texto convida a isso) parava o
polling e a lista ficava desatualizada. (4) Texto de estado removido de uma célula ficou só com
ícone `aria-hidden` — célula vazia para leitores de ecrã. (5) `reason in OBJ` aceitava
`"constructor"`.

**A regra.** Classe Tailwind nova ou alterada: confirmar que existe (`node_modules/tailwindcss/dist`
ou o CSS do build). Somas/limites de valores decimais comparam-se em inteiros (centésimas), como o
`decimal` do servidor. Um estado que continua depois de o utilizador fechar o ecrã não pode
depender de um componente montado: a lista acompanha-o sozinha (`refetchInterval` enquanto houver
linhas não terminais). Ícone de estado leva sempre texto (ou `sr-only`). Lookup em objeto por valor
externo: `Object.hasOwn`. `refetchInterval` inline com `keepPreviousData` parte a inferência do
`useQuery` — extrair para função tipada com `Query<T>`.

## 2026-10-02 — Fecho da 6E-4: rascunho preso ao modo antigo, query desativada "pending", fluxo de conflito sem saída

**O que aconteceu.** Três defeitos reais no código aplicado dos docs 03–06, todos fora do que
os testes do blueprint exercitavam:

- **D1 (treino).** O A1 (`initializedFor`) protegia o rascunho de refetches, mas quando
  `has_history` passava a verdadeiro a meio da edição (409 ou série registada no painel), datas
  e estrutura ficavam desativadas com os valores editados. O `PATCH` seguinte reenviava a data
  alterada e o servidor (`TrainingPlanStore.cs:81`) devolvia outro 409: o trainer ficava preso.
- **D2 (nutrição).** `assessment.isPending` mostrava "A carregar dados do cliente…" para sempre
  em qualquer plano existente: no TanStack Query v5, uma query desativada sem dados fica
  `pending`. E um erro na ficha do cliente não era mostrado.
- **D3 (suplementos).** Depois de reativar a atribuição existente após o 409, o formulário
  continuava em "Nova atribuição" com o erro visível; gravar de novo dava outro 409 e a dose
  escrita nunca chegava à atribuição.

**A regra.**
- Proteger o rascunho de refetches não chega: quando o servidor torna campos só de consulta,
  repor esses campos a partir do servidor (só esses) para o ecrã e o pedido seguinte não
  mentirem.
- Indicador de carregamento de uma query com `enabled` condicional usa `isLoading`
  (`isPending && isFetching`), nunca `isPending`.
- Um fluxo de recuperação de conflito tem de terminar num estado gravável: testar o passo
  seguinte ao "Reativar", não só o diálogo.

## 2026-10-02 — Validação 6E-5: upload em testes, texto do filtro, `values` do RHF

**O que aconteceu.**
- O upload multipart do logo rebentava nos testes antes de chegar ao MSW
  (`Cannot read properties of undefined (reading '_buffer')`): o `openapi-fetch` cria um
  `Request` do Node (undici) e o `FormData`/`File` do jsdom não são compatíveis com ele. Num
  browser real é a mesma implementação.
- `await screen.findByText('Em falta')` resolvia com a `<option>` do filtro de estado antes de
  a tabela carregar; a mutação FM3 sobreviveu por isso.
- O teste do rascunho da marca relia dados iguais; `useForm({ values })` só repõe quando os
  valores mudam, por isso `values` e `defaultValues` comportavam-se igual (FM8 sobreviveu).

**A regra.**
- Testes de upload: `File` de `node:buffer` (import dinâmico `import('node:' + 'buffer')`,
  porque o tsconfig da app não tem tipos do Node) e `FormData` obtido de
  `await new Response('', { headers: { 'content-type': 'application/x-www-form-urlencoded' } }).formData()`,
  instalado com `vi.stubGlobal`. No handler MSW, ler o corpo com `request.text()`.
- Esperar por dados dentro do contentor certo (`within(await screen.findByRole('table'))`)
  quando o mesmo texto existe num filtro.
- Um teste de "rascunho sobrevive ao refetch" tem de fazer a segunda leitura trazer valores
  diferentes nos campos da própria secção.


## 2026-10-04 — Fecho 6E-5: nome do parâmetro, valor da tab, diálogo que não fecha

**O que aconteceu.** Na aplicação manual do pack 6E-5 entraram três defeitos que o
typecheck e o lint deixaram passar. O CI #24 apanhou-os pelos testes do blueprint 07.
- `useCheckInListQuery` passou a enviar `page` em vez de `page_number`. O `tsc -b`
  ficou verde. O servidor ignora parâmetros desconhecidos e devolvia sempre a página 1.
- O valor da tab mudou para `check-ins` no `ClientDetailPage`, mas o link da lista global
  continuou com `?tab=checkins`, o que abria o Resumo.
- `removeLogo` perdeu o `setConfirmRemove(false)`. O `ConfirmDialog` é controlado de fora e
  não fecha sozinho, por isso ficou aberto depois do sucesso.

**A regra.**
- Não confiar no `tsc` para os nomes da query string do `openapi-fetch`. O teste da lista
  tem de ler o parâmetro do pedido no handler MSW (`searchParams.get('page_number')`), já
  na primeira página e não só depois de mudar o filtro.
- Um valor de `?tab=` vive em dois sítios: no `TABS` do detalhe e em cada link. Mudar um
  obriga a procurar `tab=<valor>` em `src/` e nos testes.
- Com `ConfirmDialog`, cada caminho de sucesso fecha o diálogo explicitamente. O teste
  confirma que `alertdialog` desapareceu.
- `setError` num campo que não está renderizado (o cliente fixo na tab ou ao reagendar)
  engole o erro. Quando o campo está escondido, mandar a mensagem para `root`.
- Numa lista filtrada em que a própria ação tira o item do filtro ("Por rever" → rever),
  esvaziar a última página deixa o ecrã vazio e sem paginação. Repor a última página que
  existe a partir do `total_count`.

## 2026-10-08 — Fecho 6F-1: operador da mistura, rota do cartão, `testid` do patch

**O que aconteceu.** Na aplicação manual do pack 6F-1 (`3111e98`):
- `mix()` em `contrast.ts` ficou com `color * (target - color) * amount` em vez de `+`. O
  `tsc` não vê nada (continua `number`) e saem hex inválidos como `#-a83-1f4-58`; 11 testes
  caíram em cascata (contrast, brandTheme, PortalLayout, SettingsPage).
- A ação "Responder" do Início apontava para `/portal/check-in`. O router só tem
  `check-ins` e o `*` faz `Navigate to="/"`, por isso o cliente voltava ao Início sem erro.
- O patch do `SettingsPage.test.tsx` ficou com `brand-preview-${theme}` onde o blueprint tem
  `brand-preview-header-${theme}`: o cartão inteiro não tem a cor do cabeçalho.
- Aspas duplas, `if` partido e JSX desalinhado em 9 ficheiros: o Prettier falhava.

**A regra.**
- Depois de aplicar um pack, correr o diff programático contra o `manifest.json` ignorando
  comentários: qualquer linha de código diferente é defeito ou desvio a justificar.
- Um link interno só fica provado seguindo-o até ao ecrã de destino (o `*` engole rotas
  erradas). Os cartões do Início têm agora um teste que clica cada ação.
- `npx prettier --write` nos ficheiros do pack antes do commit; o `--check` local dá falsos
  avisos de CRLF (usar `--end-of-line auto` para confirmar).
