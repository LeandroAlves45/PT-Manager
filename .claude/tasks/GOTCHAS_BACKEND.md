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
