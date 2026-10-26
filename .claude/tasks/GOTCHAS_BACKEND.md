# Gotchas — Backend

Padrões de erro identificados depois de correções do utilizador, no backend (C# / .NET / EF Core / testes).
Entradas novas: registar de imediato, antes de continuar. O antigo `correction.md` foi migrado para aqui e apagado em 2026-09-29 (histórico no git).

## 2026-09-20 — Classificar um teste intermitente como "flakiness conhecida" em vez de investigar

**O que aconteceu.** Ao fechar a Sprint 6B, sete testes
`*_WithoutToken_ReturnsUnauthorized` falharam com 429 numa corrida integral. Verifiquei que
os ficheiros estavam intocados pela sprint e que passavam em isolamento, concluí que era
flakiness pré-existente do rate limiter, documentei-a como tal e dei a sprint por fechada.
O utilizador correu a suite no terminal, teve 6 falhas do mesmo tipo, e pediu explicação.

**O que estava errado.** Havia uma causa raiz concreta e corrigível: o `TestServer` do
`WebApplicationFactory` não preenche `RemoteIpAddress`, por isso o `IpKey` do limitador
global caía sempre em `"ip:unknown"` e toda a suite partilhava um orçamento de 60 pedidos
anónimos por minuto. Nada de aleatório — um recurso partilhado com orçamento, mais
paralelismo. A correção foram 40 linhas no host de teste, sem tocar em produção.

**O padrão.** "Passa em isolamento, falha em conjunto" e "o conjunto de testes afetados muda
a cada corrida" não são sinais de ruído: são a assinatura de um recurso partilhado com
limite. `NAO` registar como flakiness conhecida sem antes procurar o recurso partilhado.

**A regra.** Perante um teste intermitente:
1. Identificar o que os testes afetados têm em comum (aqui: pedido anónimo ao host completo).
2. Procurar estado partilhado entre eles — contadores, orçamentos, caches, partições, ligações.
3. Só depois de encontrar o recurso e provar que não é controlável é que se classifica como
   flakiness. E mesmo aí, dizê-lo ao utilizador em vez de o enterrar num relatório.

Nunca fechar uma sprint como "tudo verde" tendo visto falhas numa corrida integral, mesmo que
outra corrida passe.

## Mutação restaurada na fonte, mas viva no bin dos testes (Sprint 6E-3, 2026-09-29)

**O que aconteceu.** Validei as mutações B1/B2/B4 recompilando só
`Api.FunctionalTests` e restaurei cada ficheiro com `cp`. Depois compilei apenas
`src/Api/Api.csproj` (para o contrato) e corri `dotnet test PTManager.sln --no-build`: o
teste novo falhou com `managed_video_status = null` — era o `Api.dll` da mutação B4,
copiado para `tests/.../bin/Release`, que nenhum build posterior tinha substituído.

**A regra.** Depois de restaurar uma mutação, `dotnet build PTManager.sln -c Release`
completo antes de qualquer `dotnet test --no-build`. Uma falha que só aparece na corrida
integral logo após mutações é primeiro suspeita de binário obsoleto.

## Restaurar uma mutação com o mtime antigo engana o build incremental (Sprint 6E-4, 2026-10-01)

**O que aconteceu.** O harness guardou o ficheiro com `shutil.copy`, aplicou a mutação, compilou
`Api.FunctionalTests` em Release e depois restaurou o original com `shutil.move` da cópia. A
cópia tinha um mtime anterior ao `Infrastructure.dll` mutado, e por isso o
`dotnet build PTManager.sln -c Release` completo seguinte considerou o projeto atualizado. A
corrida integral falhou com `client_name = "x"`, que era a mutação MU6. A fonte estava correta e
o binário não.

**A regra.** A regra de 6E-3 (build completo depois de mutações) não chega: depois de restaurar
mutações, `dotnet build ... --no-incremental` (ou `touch` nos ficheiros restaurados) antes de
`dotnet test --no-build`. Restaurar escrevendo o conteúdo original (novo mtime) em vez de mover
uma cópia antiga.

## Uma regra de negócio lida pela UI e imposta pelo servidor vive num só sítio (Sprint 6E-4, 2026-10-01)

**O que aconteceu.** O blueprint calculava `has_history` em `TrainingPlanQueries` com uma cópia
literal do predicado de `TrainingPlanStore.HasLogsAsync`, que devolve o 409
`training_structure_has_history`. As duas cópias eram iguais nesse dia, mas uma alteração a uma
só faria a UI anunciar como editável um plano que o servidor recusaria.

**A regra.** Quando a UI pré-anuncia o resultado de um guard (flag `has_*`, `can_*`), a leitura
e o guard compõem a mesma consulta (`TrainingPlanHistory.PlanIdsWithHistory`, um `IQueryable`
que o EF inlina). Cada ramo do predicado tem um teste funcional que o mata.

## Parâmetro opcional num record de resposta torna o campo opcional no OpenAPI (Sprint 6E-4, 2026-10-01)

**O que aconteceu.** `string? ClientName = null` no fim de `ClientSupplementAssignmentResponse`
gerou `client_name?: null | string` no `schema.d.ts`, o que obriga o frontend a tratar
`undefined` e `null`.

**A regra.** Records de resposta não têm parâmetros com valor por omissão. Campo anulável é
passado explicitamente (`ClientName: null`), e o contrato fica `client_name: null | string`.

## Patch de testes aplicado só em parte: o teste falha por configuração, não por código (Sprint 6F-1, 2026-10-07)

**O que aconteceu.** Na aplicação do doc 02 da 6F-1, o seeder e as options entraram inteiros, mas
o patch de `DevelopmentSeedTests` só entrou nos primeiros hunks: faltava
`DevelopmentSeed:SecondClientEmail` em `SeedSettings`, os helpers e três testes. O seed criava
`cliente2@ptmanager.local` (valor por omissão) e o teste fazia login com `cliente2@seed.test`,
o que dava 401.

**A regra.** Uma nova propriedade em `*Options` com valor por omissão exige a chave
correspondente nos settings explícitos dos testes. Depois de aplicar um patch, comparar
`git diff --stat` com o tamanho do bloco do blueprint: uma diferença grande nas linhas `+`
denuncia hunks em falta antes de correr qualquer teste.

## Decimal sem máximo no validador: um valor fora da coluna dá 500 (Sprint 6F-2, 2026-10-09)

**O que aconteceu.** Na validação dos blueprints da 6F-2, um `weight_kg` de `123456789012` no
`POST /portal/exercise-set-logs` devolveu 500. O validador só exigia `≥ 0` e a coluna é
`numeric(10,2)`: o overflow rebentava no `SaveChanges`. Os quatro validadores de séries
(trainer e cliente, registar e corrigir) tinham o mesmo buraco.

**A regra.** Todo o `decimal` de entrada tem mínimo **e** máximo no validador, abaixo do limite
da coluna. O máximo vive como constante no Domain (`ClientExerciseSetLog.MaxWeightKg = 1000`),
o Domain também o impõe e os validadores referem a constante. Ficam por rever no mesmo padrão
os pesos planeados e os das avaliações (backlog).

## 2026-10-11 — Blueprints 6F-4: validador mais largo do que o domínio (500 no peso corporal e no email)

**O que aconteceu.** Dois pedidos passavam o FluentValidation e rebentavam a seguir:
`weight_kg: 999999999` na resposta ao check-in (coluna `numeric(10,2)`) e
`contact_email: "a@b"` no perfil do portal (`.EmailAddress()` aceita, o value object
`EmailAddress` lança `DomainException`, que a API não mapeia). Provado com um teste funcional
temporário contra o PostgreSQL.

**A regra.**
- Todo o `decimal` de entrada tem máximo abaixo do limite da coluna (repetição da regra de
  2026-10-09). Uma coluna JSON não rebenta, mas grava lixo: decidir o teto na mesma.
- Quando o domínio tem um value object para um formato, o validador chama a regra do value
  object (`EmailAddress.IsValid`), nunca uma regra parecida de biblioteca.
- Com `RuleForEach` sobre um array inline, o `errors[].field` é a posição
  (`BodyMeasurements[2]`), não o nome do campo: fixar com um teste funcional se um cliente da
  API depender disso.
- `WithErrorCode` só se aplica à regra imediatamente anterior: para um intervalo com um só
  código, usar um `Must`.

## 2026-10-13 — Fecho 6F-4: escala do decimal, carácter nulo e leitura sem `IsActive`

**O que aconteceu.** Três achados do agente de segurança, provados com sondas funcionais contra
o PostgreSQL antes de corrigir:
- `weight_kg: 0.004` e `body_fat_percentage: 99.996` passavam o validador (`> 0`, `< 100`),
  a coluna `numeric(10,2)` arredondava para `0.00` e `100.00` e o check da tabela rebentava:
  500. Com `71.555` a escrita passava, mas a resposta devolvia o valor por arredondar e o
  reenvio igual dava 409.
- `"notes": "a\u0000b"` e `"phone": "+351\u0000911"` davam 500: o PostgreSQL não guarda
  U+0000 em `text` nem em `jsonb`.
- `GetMyAsync` e `ListMyActiveAsync` dos suplementos do portal não filtravam
  `client.IsActive`: um cliente arquivado, com o token ainda válido, lia as atribuições e as
  notas do personal trainer.
- À parte, o diff contra o blueprint mostrou um `$` em falta numa mensagem interpolada de
  `InitialAssessment`. Compilava e nenhum teste olhava para a mensagem.

**A regra.**
- Um `decimal` de entrada valida o intervalo **e** a escala da coluna
  (`AssessmentLimits.FitsDecimalPlaces`). Um intervalo aberto (`> 0`, `< 100`) mais
  arredondamento na coluna dá um valor fora do check.
- Texto livre vindo de um cliente leva `.WithoutNullCharacter()`
  (`Application/Validation/TextValidationRules.cs`, código `text_invalid_character`). Foi
  aplicado aos validadores do portal; os do personal trainer ficam no backlog.
- Toda a leitura do portal parte de um cliente ativo. Ao escrever uma query nova com
  `client.UserId == userId`, juntar `client.IsActive` ou usar `PortalClients.FindActiveIdAsync`.
- Um teste de limite usa o valor literal (`1000`, `1000.01`), não a constante do domínio: com
  a constante, o teste acompanha qualquer alteração e nunca falha.
- Uma exceção com mensagem interpolada merece uma asserção sobre a mensagem.

## 2026-10-14: blueprints da 6GA (Auth UX)

**O que aconteceu.**
- U+0000 no email ou no nome dava **500 anónimo** em signup, login, reposição e reenvio da
  confirmação: `EmailAddress.IsValidEmail` só recusava espaços, e o PostgreSQL recusa NUL em
  `text`. Provado por sonda funcional (`AuthNullCharacterTests`, 5/5 a 500 antes).
- `.EmailAddress()` do FluentValidation ainda estava em login, convite, reposição e signup
  (`a@b` passava e o `new EmailAddress(...)` lançava).
- `AdminModerationQueueControllerTests` falhava 3 em 5: assumia o alimento do trainer na
  página 1, com 52 alimentos "Portal rice" do mesmo `UpdatedAt` na suite.

**A regra.**
- Toda a validação de email passa por `EmailAddress.IsValid`, que agora recusa
  `char.IsControl`. Nomes e outro texto livre anónimo levam `.WithoutNullCharacter()`.
- Um teste funcional que procura um item numa listagem partilhada pela suite percorre as
  páginas; nunca assume a página 1.
- Signup, reenvio e recuperação respondem igual para email existente e inexistente
  (`AuthEnumerationSafetyTests`); o que muda é só o email enviado.

## 6GA · aplicação do blueprint perdeu uma linha (2026-10-15)

- `RegisterTrainerCommandValidator` ficou sem `.WithoutNullCharacter()` em `FullName`:
  500 anónimo e diferença entre email novo e existente. Apanhou-o `AuthNullCharacterTests`.
- Depois de aplicar um pack, comparar os ficheiros com o `manifest.json` por hash antes de
  correr a suite; o diff por linha do `.md` encontra a linha em falta em segundos.
- Reemissão de token pedida sem sessão tem cooldown (60 s, `RecentlyIssued`): sem ele, um
  terceiro invalida o link pendente da conta.

## 2026-10-19 — 6GB-SU: teste de Development herdava `DevelopmentEmail:LogLinks` dos User Secrets

- `LogLinksOff_InDevelopment_StillSendsThroughTheProvider` falhava também no `main` limpo (worktree).
  Sem entrada no `appsettings`, o teste passou com `DevelopmentEmail__LogLinks=false` no ambiente:
  o `true` vinha dos User Secrets da máquina. Mesma classe do `DevelopmentSeed:Enabled`.
- **Regra:** um teste que corre o host em Development fixa em `AdditionalSettings` todas as opções
  de que depende; nunca confia no valor por omissão. Provar a origem com a variável de ambiente,
  sem abrir os segredos.
- Admin sem tenant a escrever `TrainerSubscription`: o interceptor exige tenant (Política A'). A
  isenção usa uma exceção explícita (superuser administrativo, sem tenant, auditoria
  `trainer_subscription` obrigatória, dono imutável), como a moderação do catálogo privado.

## 2026-10-21 — 6GB-TR

- Convite: o token é gravado (e o anterior anulado) antes do envio do email. Com o «Copiar link», um
  email falhado deixava a ficha a dizer «Convite enviado» sem link. **Regra:** depois de um commit,
  uma falha externa (email) não pode deixar o estado gravado sem saída; aqui devolve-se o link com
  `email_sent: false`.
- A API a correr (`preview_start api`) bloqueia as binárias de Debug: `dotnet build` dos testes falha
  com «2 Error(s)» sem mensagem útil. **Regra:** parar a API antes de compilar ou de regenerar
  snapshots; voltar a arrancá-la para o `api:types`.
- Testes funcionais com Testcontainers falham todos ao mesmo tempo com `DockerUnavailableException`
  quando o Docker Desktop para (aconteceu durante uma pausa por limite). Confirmar `docker ps` antes
  de investigar código.

## 2026-10-22 — 6GB-CL

- Testes de migrations antigas (`AddManagedImageAssetsMigrationTests`) semeavam a ficha com o modelo
  EF atual num schema antigo: a coluna nova `clients.selected_meal_plan_id` partiu-os (42703).
  **Regra:** num teste que migra para trás, semear por SQL só com as colunas que existiam nessa
  migration.
- `Error.Create` recusa a categoria `Validation` («Use Validation method»): erros de validação de
  domínio fazem-se com `Error.Validation([new ValidationError(field, code, message)])` e saem como
  `errors[]`, não como `title`.
- Corrida entre um `ExecuteUpdate … WHERE EXISTS(...)` (escolher plano) e uma transação que muda a
  linha referida (arquivar): em READ COMMITTED o `EXISTS` vê o estado antes do commit. **Regra:**
  limpar o estado derivado também no caminho inverso (reativar), em vez de confiar na ordem.
- `TimeZoneInfo.ConvertTimeToUtc` de datas arbitrárias vindas do pedido: calcular o intervalo UTC só
  depois de a data estar validada contra o domínio (dia do plano). No Windows o fuso de Berlim não tem
  LMT; no Linux (Render/CI) tem, e o comportamento difere.
- Depois do reinício da máquina o Docker Desktop fica parado e o daemon responde 500 enquanto arranca:
  esperar `docker ps` listar o contentor antes dos testes funcionais.

## 2026-10-24 — Bloco pré-Sprint 7 (RN-013)

- `ExecuteDelete`/`ExecuteUpdate` num store administrativo (superuser, sem tenant) sobre entidades
  com Global Query Filter não afetam nenhuma linha e não dão erro: o filtro esconde tudo. Desbloquear
  um alimento não o tirava dos planos; só um teste funcional de ponta a ponta o mostrou.
  **Regra:** em escritas sem tenant, `IgnoreQueryFilters()` explícito em cada query (também nas
  subconsultas `Any`), com o filtro pelo id do recurso.
- Testes de login com um personal trainer do `TrainerTenantSeeder`: o email não está confirmado e a
  palavra-passe é opaca, por isso a entrada falha com 401 genérico. Confirmar o email e gravar o hash
  real com `IPasswordHasher<User>` antes de testar a entrada.
- Heredocs longos no Git Bash voltam a partir com aspas e `$` em código C#: escrever o script com a
  ferramenta Write e corrê-lo com node (o `python3` do Git Bash é o atalho da Store e encrava à
  espera do stdin).
- Template de email com `Replace` em cadeia: o valor inserido por um placeholder volta a ser lido
  pelos seguintes. Um nome escrito «{{x}}» fazia a verificação de placeholders por resolver lançar e
  o lembrete falhava sempre. **Regra:** substituição numa só passagem (`Regex.Replace` com avaliador
  sobre o template), com teste de um nome igual a um placeholder.
- Campo de texto livre que passa a ser escrito pelo cliente (avaliação inicial no portal): rever o
  validador partilhado, que vinha só do personal trainer. `Goals` e `MedicalConditions` não tinham
  máximo; ficaram com 2000, como os textos da nutrição (`goals_too_long`,
  `medical_conditions_too_long`).

## 2026-10-25 — Sprint 7A (observabilidade)

- **O Sentry ignora os filtros de categoria do appsettings.** O `SentryLoggerProvider` tem uma
  regra de filtro própria, específica do provider, que ganha a `Microsoft.AspNetCore: Warning`.
  Logs de framework com URL viram breadcrumbs. **Regra:** `SetBeforeBreadcrumb` corta a query.
- **Ignorar o `traceparent` exige dois propagators.** O hosting usa o `DistributedContextPropagator`
  do DI. A instrumentação de ASP.NET Core do OpenTelemetry volta a extrair com
  `Propagators.DefaultTextMapPropagator` e recria a span com o pai remoto. **Regra:** registar os
  dois (`InboundTraceContextIgnoringPropagator` e `…TextMapPropagator`).
- **Pai remoto sem listener.** O hosting usa `new Activity` + `SetParentId(string)`, e o
  `HasRemoteParent` não chega. O sinal fiável é o `ParentId` da raiz local.
- **API em background no Windows.** O `TaskStop` da shell não mata o `dotnet Api.dll` filho:
  continua na porta e bloqueia a DLL do build seguinte. **Regra:** confirmar com
  `Get-NetTCPConnection` e terminar pelo PID depois de verificar que o módulo é o da worktree.
- **Variáveis com ponto no Bash.** `export Logging__LogLevel__Microsoft.EntityFrameworkCore=…`
  falha. Usar `env "NOME=valor" dotnet …`.
- **O CORS só aceita origens HTTPS** (`Cors:AllowedOrigins`). Testes de carga com `Origin:
  https://localhost:5173`.

## 2026-10-26 — Fecho do Sprint 7A (aplicação na `main`)

- **Nome de instrumento mudado à mão parte o teste em silêncio.** Na aplicação, o histograma
  passou de `ptm.http.db_commands` para `ptm.http.db.commands`. O `MetricCapture` filtra pelo
  nome exato e o teste da rota deixou de ver medições. No Prometheus os dois nomes dão
  `ptm_http_db_commands`, por isso nem o painel avisaria. **Regra:** nomes de métricas e de
  fontes são contrato (docs 04 e 07, alertas, testes); mudar só com o teste e os docs no mesmo
  commit. Reposto o nome do blueprint.
- **Reescrever comentários de blueprints introduz erros de domínio.** «DNS» em vez de «DSN» no
  Sentry, «escritório» em vez de «critério», `using`s sem uso. O build não os apanha (o IDE0005
  não falha o build). **Regra:** depois de aplicar um pack, conferir os 45 hashes e ler o
  `git diff -w <commit testado>` antes do commit.
- **User Secrets ligam a telemetria nos testes Development.** Com `Observability:Enabled` e
  `Sentry:Enabled` nos secrets (preparação das contas reais) e sem `OTEL_EXPORTER_OTLP_ENDPOINT`,
  23 testes funcionais com host `Development` (OpenAPI, seed, email dev) falharam no arranque.
  Mesmo padrão do seed (`gotcha_user_secrets_in_test_hosts`). **Regra:** a
  `ApiWebApplicationFactory` desliga as duas integrações só em `Development`; em `Testing` o
  `ByDefault_NoExporterAndNoSentryAreRegistered` continua a provar o `appsettings.json`
  (mutações MOFF1/MOFF2).
