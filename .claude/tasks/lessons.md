# Lessons

Lições capturadas depois de correções do utilizador.

## 2026-09-18 — Sprint 6B (planeamento e blueprints)

1. **Enums em query string ligam pelo nome do membro.** `?status=pending_review` devolve 400
   mesmo com `JsonStringEnumConverter(SnakeCaseLower)` configurado (isso só afeta o corpo JSON).
   Filtros novos: membros de uma palavra, e um teste funcional que prova 200 no válido e 400 no
   inválido.
2. **O EF Core não ordena por membro de um record construído na projeção.** Projetar para tipo
   anónimo, ordenar em SQL e construir o record em memória. Só se apanha a correr contra
   PostgreSQL real.
3. **Filtro explícito de tenant numa query é defesa em profundidade.** Uma mutação que o desligue
   pode sobreviver porque o Global Query Filter já isola. Mutações de isolamento têm de desligar
   a camada que isola de facto.
4. **Ficheiros grandes vão para blueprint por excerto** (> 200 linhas): âncora com contexto
   inalterado, bloco completo, marcadores `// [fase] NOVO/ALTERADO/REMOVIDO` e validação por
   inclusão literal no ficheiro materializado.

## 2026-09-20 — Sprint 6B (implementação real)

5. **`ConvertTimeToUtc` no lugar de `ConvertTimeFromUtc` lança, não devolve errado.** O overload
   `TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)` atira `ArgumentException` quando o
   `Kind` é `Utc` e o fuso não é `TimeZoneInfo.Utc`. Num helper que força `Kind=Utc` antes, é
   falha garantida em runtime. Como a conversão vive num único sítio (`LocalDates`), um
   caractere derrubava cinco endpoints. Todo o helper de data merece um teste que fixe o
   sentido da conversão num fuso com offset diferente de zero.
6. **Diff de blueprints tem de ser programático e normalizado.** Dos 81 blocos de produção da
   6B, 23 diferiam; só três eram defeitos. Sem remover comentários e diferenças de quebra de
   linha antes de comparar, os defeitos reais ficam invisíveis no ruído.
7. **Argumentos do mesmo tipo trocados não dão erro de compilação.**
   `IssueClient(trainerId, clientUserId)` em vez de `IssueClient(clientUserId, trainerId)`
   compilou e deu um 500 opaco. Asserções funcionais devem incluir sempre o corpo da resposta
   na mensagem de falha — sem isso é preciso montar um probe com `ILoggerProvider` para ver a
   exceção do servidor.
8. **Blueprints validados numa materialização histórica não compilam necessariamente hoje.**
   Seis ficheiros do doc 09 precisaram de correção (usings, `[Collection]`, campo vs
   propriedade). Contar com isso no orçamento da fase de implementação.
9. **Test doubles com `throw new NotImplementedException()` gerado pelo IDE são dívida
   silenciosa.** Compilam e só rebentam quando alguém lá chega. Ao alterar a assinatura de uma
   porta, varrer os doubles à procura deles em vez de deixar o compilador "resolver".
10. **Ao inserir um registo no meio de um bloco de DI, confirmar que não se duplicou o par
    seguinte.** `AddApplication_RegistersEachServiceTypeExactlyOnce` apanhou 82 validators onde
    deviam ser 80.
11. **Restaurar um ficheiro com `mtime` antigo engana o build incremental.** Depois de correr
    mutações, o MSBuild deu os projetos por atualizados e a corrida seguinte usou DLLs com o
    código mutado — cinco falhas sem qualquer resíduo no `git diff`. Guiões de mutação têm de
    fazer `touch` no restauro, ou o build seguinte tem de ser `--no-incremental`.
12. **`TestServer` não preenche `RemoteIpAddress`, e isso metia a suite toda numa partição do
    rate limiter.** Com `IpKey` a cair em `"ip:unknown"`, todos os pedidos anónimos do
    `WebApplicationFactory` partilhavam um orçamento de 60/min, e os testes
    `*_WithoutToken_ReturnsUnauthorized` recebiam 429 em vez de 401 de forma não determinista.
    Corrigido com um IP por `HttpClient` (`ConfigureClient` + `IStartupFilter` que traduz
    `X-Test-Client-Ip`), sem tocar na configuração de produção. Regra geral: quando um teste de
    host completo depende de algo derivado da ligação (IP, porta, certificado), confirmar que o
    `TestServer` o preenche — muitas vezes não preenche, e o valor por omissão é partilhado por
    todos os testes.
13. **Um teste que passa "quase sempre" é um teste dependente de corrida, não ruído.** Foi
    descartado como flakiness pré-existente numa primeira análise; o conjunto de testes afetados
    mudar a cada corrida era precisamente a pista de que havia um recurso partilhado com
    orçamento. Vale a pena perseguir a causa em vez de a registar como conhecida.
14. **Estado de módulo sobrevive entre testes do mesmo ficheiro.** No frontend, o
    `inFlightRefresh` de `client.ts` e o canal de `session-events.ts` são singletons: um teste
    que deixa um refresh pendurado bloqueia os seguintes. Todo o teste que suspende o arranque
    tem de o libertar antes de terminar; para simular separadores usar `vi.resetModules()`.
15. **`execFileSync('npx.cmd')` falha no Windows com Node ≥ 20 (EINVAL).** Scripts Node do
    frontend chamam CLIs por `process.execPath` + caminho do `bin` em `node_modules`, nunca por
    `npx`/`.cmd` sem shell. Um check que "falha" pode estar só a rebentar — confirmar a mensagem.

## 2026-09-24 — Sprint 6D (fecho frontend admin)

- Aplicação manual de blueprints introduz desvios silenciosos (limite 100 vs 1000, ramo de UI
  apagado). O diff programático bloco ↔ ficheiro apanhou os dois em segundos; manter no fecho.
- Antes de adiar uma feature, verificar se o backend já a expõe: a gestão de vídeo só faltava UI.
- Testes frontend com upload a terceiros: XHR falso via `vi.stubGlobal`, não MSW (o `File` do
  jsdom falha na ponte para `Request`); `<video>` do jsdom precisa de spies para metadados.
- Validação nativa (`max`) chega antes do Zod: testar a mensagem Zod pelo caminho real.

## 2026-09-24 — Sprint 6E-1 (blueprints frontend trainer)

- **nuqs lê `window.location`, não o router em memória.** Com `createMemoryRouter`, filtros e
  flags na query string (`?new=true`) nunca chegavam ao ecrã e o URL escrito por um teste
  contaminava o seguinte. `renderApp` sincroniza `window.history` com a entrada inicial.
- **Nada além do texto do rótulo dentro de `<label>`.** Erros e `<option>` entram no nome
  acessível; ligar por `htmlFor` e o erro por `aria-describedby` (`FormField`).
- **Substituir um placeholder de rota parte testes antigos** que dependiam do título ou de
  regexes largas; procurar o texto do placeholder em `src/test` antes de trocar a rota.
- **Falha que passa isolada = estado partilhado até prova em contrário**: timestamps fixos em
  dados de teste + ordenação por GUID tornam a paginação não determinista.


## 2026-09-25 — Site de marketing

- Copy de marketing gerada (Claude Design) promete funcionalidades que não existem: verificar cada frase contra o backend antes de publicar e proteger com um teste de conteúdo.
- Barras invertidas em strings escritas via Bash heredoc ou Edit podem ser interpretadas (`\u003c` virou `<` e anulou um escape de XSS). Em código de segurança, testar o escape e, se preciso, construir o carácter com `String.fromCharCode`.

## 2026-09-25 — Fecho da 6E-1

- **Um pack de blueprints só está aplicado quando o doc de testes também está.** Aplicar o doc 07
  por último e correr a suite antes do push: uma rota nova que faz pedidos parte testes antigos
  que a visitam (MSW `onUnhandledRequest: 'error'` → timeout).
- **Valor por omissão num `<select>` de dado clínico é uma decisão, não um detalhe**: um
  "Feminino" pré-escolhido gravava-se sem escolha. Opção vazia desativada + Zod a recusar.
- **Estados vazios dependem da página**: com paginação, "lista vazia" ≠ "não há dados"; testar a
  página além da última.
- **Contagens vindas do backend podem ser 0 ou 1**: nunca fixar o plural.

## 2026-09-27 — Blueprints da 6E-2

- **`IgnoreQueryFilters()` numa subquery desliga os filtros globais da query inteira** (EF Core).
  Numa projeção com subquery para ler um nome, nunca o usar: packs cancelados voltaram a aparecer.
  Se for preciso ignorar filtros, fazê-lo numa query separada.
- **Dois records com o mesmo nome curto em namespaces diferentes colidem no OpenAPI**
  (`SessionResponse` de auth e de sessões): o documento funde-os sem aviso. Ao criar contratos,
  procurar o nome em `Api/Contracts/**` antes.
- **Enums em query string**: resolvido globalmente no blueprint 6E-2 (`QueryEnumModelBinderProvider`);
  até ser aplicado, a lição 1 continua válida no repo real.
- **Mutações sobre refetch do TanStack**: o structural sharing mantém a referência de `data`
  quando o conteúdo é igual; um teste de "refetch" só prova algo se a 2.ª resposta for diferente.
- **Excertos de ficheiros gerados/repetitivos**: âncoras únicas no ficheiro inteiro nem sempre
  existem; aplicar por ordem, com a âncora procurada a partir do excerto anterior.

## 2026-09-27 — comandos `dotnet ef` com ambiente descartável
- Nunca encadear `docker run ... && cat > env` antes de um `dotnet ef`: se o primeiro passo
  falha, o `ef` corre sem as variáveis e cai na BD dev (user secrets). Usar `set -e`, fazer
  `echo` da connection string e só depois o `ef`.
- Harness de mutações repõe ficheiros inteiros: avisar o utilizador antes, para não sobrescrever
  edições feitas em paralelo no IDE.

## 2026-09-29 — Fecho da 6E-2

- **Doc de testes fora do commit de implementação, segunda vez**: a regra do doc 01 ("mesmo
  commit") não chega; antes do push, `npm run test:run` localmente é obrigatório.
- **Pré-escolha vinda de query dependente**: bloquear o submit enquanto a query está pendente,
  senão o formulário envia o valor vazio em silêncio.
- **Fuso dos testes fixo** (`Europe/Lisbon`) e datas de verão nos testes de offset; em UTC um
  `Z` passa por offset local.
- **Mutação equivalente**: antes de escrever um teste para uma sobrevivente, confirmar na lib
  (ex.: nuqs `clearOnDefault`) se o comportamento é mesmo diferente.

## 2026-09-29 — Blueprints da 6E-3

- **Debounce só para escrever**: apagar a pesquisa (trocar de tab, "Limpar filtros") tem de ser
  imediato, e o debounce vive no componente que desmonta com o contexto (cada tab), não na página.
  Testar os parâmetros do **primeiro** pedido do ecrã novo.
- **Validação de objeto (`.refine`) só com os campos válidos**: `Number("abc")` é NaN e a regra da
  soma escrevia por cima do erro do campo.
- **Recurso substituído invalida as caches derivadas** (URL assinado de reprodução), não só a lista.
- **`role="combobox"` e opções cmdk**: nome pelo `<label>`; estado da opção por `aria-label`, nunca
  `aria-selected` (é o foco do cmdk).
- **Mutações de backend com a API da worktree a correr**: a DLL fica bloqueada, o build falha e o
  harness conta "morte" sem nome de teste. Parar a API antes; morte sem nome não conta.
- **Prettier numa pasta inteira no Windows** reescreve fins de linha de ficheiros não tocados;
  formatar só os ficheiros alterados (lista do `git status`) e repor os que só mudaram EOL.
- **Confirmar no código o que um agente de exploração resume**: o exemplo de ordem canónica
  ("quadriceps,glutes") estava errado; o teste apanhou-o.

## 2026-09-30 — Fecho da 6E-3

- **SHA-256 normalizado + diff de blocos** encontrou em minutos os 5 desvios com efeito entre ~30
  ficheiros só com formatação/comentários; classificar cada um (defeito / copy aprovado /
  comentário) antes de tocar.
- **Desvio de copy do utilizador que parte um teste do blueprint**: perguntar (repor vs. adaptar),
  não decidir sozinho.
- **Mutação que sobrevive por acaso dos dados do teste** (N3): procurar por força bruta valores que
  distingam o mutante (script Node) em vez de declarar equivalente.
- **Mutação corrida contra o ficheiro de teste errado** parece "viva" (M24): confirmar primeiro que
  teste a mata no doc 08.
- **Harness de mutações no Windows**: a escrita de restauro pode falhar com `Errno 22` (ficheiro
  bloqueado pelo Vitest); tentar de novo e **verificar** o conteúdo restaurado; nunca editar
  ficheiros enquanto o harness corre.
- **Mutações não correm o typecheck**: depois de refatorar o código de uma correção, repetir as
  mutações dessa correção e o `typecheck`.

## 2026-10-01 — Revisão do pack 6E-4 (codex) e testes do backend

- **Rever um pack de outro agente contra o código real**, não contra o próprio pack: o pack 6E-4
  estava coerente consigo mesmo e ainda assim duplicava uma regra do servidor (M1) e tinha um
  bug de UI que os seus testes não viam (A1).
- **Um teste de "refetch não apaga o rascunho" tem de devolver dados diferentes.** O TanStack
  Query mantém a referência quando o refetch traz JSON igual (*structural sharing*), e assim o
  `useEffect([data])` nem corre. A mutação FM1 sobreviveu até o mock passar a responder
  `has_history: true` depois do 409, que é o caso realista.
- **Patches em Markdown perdem o espaço das linhas de contexto vazias.** Materializar com
  `git apply --recount` e repor o espaço, ou gerar os patches a partir de `git diff` real.
- **Restaurar mutações com mtime novo** (ver `GOTCHAS_BACKEND.md`): uma falha integral logo após
  mutações é primeiro suspeita de binário obsoleto.

## 2026-10-02 — Fecho da 6E-4

- **Proteger o rascunho não chega quando o modo muda.** O A1 impedia o refetch de apagar edições,
  mas o histórico tornava datas/estrutura só de consulta e o ecrã continuava a mostrar (e a
  enviar) valores que o servidor recusa. Ao bloquear campos, repô-los a partir do servidor.
- **`isPending` não é "a carregar"** numa query com `enabled` condicional: fica verdadeiro para
  sempre. Indicadores de carregamento usam `isLoading`.
- **Testar o passo depois da recuperação**: o teste do 409 parava no clique "Reativar" e não via
  que gravar a seguir repetia o conflito.
- **Antes de `npm ci`, verificar watchers do utilizador** (`Get-CimInstance Win32_Process`):
  um Vitest em watch prende binários nativos e o `npm ci` deixa `node_modules` partido.

## 2026-10-02 — Sprint 6E-5 (blueprints)

- **Fixtures com valores inventados escondem defeitos.** `tier: 'BASIC'`/`status: 'active'`
  nunca existiram no backend (`FREE/STARTER/PRO`, `ACTIVE/...`) e o `SubscriptionCard`
  passou três fases a mostrar "Pagamento por regularizar" sempre. Fixtures tipadas pelo
  `schema.d.ts` não chegam quando o campo é `string`: copiar os valores dos value objects do
  domínio e ter um teste com o valor real.
- **Idempotência que depende de estado do browser parte no redirect.** Uma chave em memória
  perde-se ao voltar de uma página alojada (Stripe). Antes de pensar em storage, ver se o
  servidor consegue reconhecer a mesma operação por outra chave natural (trainer + plano + sessão
  aberta).
- **Mutação sobrevivente = teste fraco até prova em contrário.** FM3 e FM8 sobreviveram por
  testes que passavam pelo motivo errado; nenhuma era equivalente.


## 2026-10-04 — Fecho 6E-5 e do Sprint 6E

- **Os testes do blueprint são a rede da aplicação manual.** Os três defeitos da 6E-5
  (`page`, valor da tab, diálogo do logo) passaram typecheck e lint e só os testes do
  blueprint 07 os apanharam, no CI. Correr `vitest` antes do push, sempre.
- **Diff programático primeiro, leitura depois.** Comparar o SHA do manifest e os blocos dos
  docs com o disco separou 20 ficheiros divergentes em "copy do Leandro" (fica) e em três
  defeitos, em minutos.
- **Copy reescrita pelo utilizador fica, mas os erros de português corrigem-se** ("uma valor",
  "respostas" por "repostas", género de "logo", WebP/WEBP).
- **Polling limitado conta tentativas, não sucessos.** `dataUpdateCount` não sobe com o
  servidor em baixo. Somar `errorUpdateCount` e extrair a decisão para uma função pura
  testável, sem esperas reais de 30 s.

## 2026-10-04: planeamento da 6F e blueprints da 6F-1

- **Mudar a home de um papel parte os testes de redirect das outras features.** Antes de
  trocar o destino de `homeRouteFor` ou o index de uma área, procurar a rota antiga em
  `src/test`. Acrescentar handlers MSW por omissão para os pedidos da nova home.
- **O "hoje" do seed de desenvolvimento é UTC; o do portal é o fuso do trainer.** Dados que o
  portal filtra por "hoje" usam `LocalDates.Today` com o fuso das definições.
- **No Vitest com `css: false`, um `import '….css?raw'` devolve vazio.** Para ler CSS num
  teste: `import('node:' + 'fs')` e um caminho relativo a `frontend/`.
- **Depois de mutações no backend, reconstruir** antes do gate final: os binários Release
  ficam com a última mutação aplicada.
- **O hook `PreToolUse` bloqueia comandos de shell com palavras SQL** (`truncate` é também
  classe Tailwind). Escrever ficheiros com Write e correr scripts a partir de ficheiro.
