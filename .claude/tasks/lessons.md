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
