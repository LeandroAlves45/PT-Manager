# Sprint 6B — fecho no backend real (2026-09-20)

## Checklist

### Fase 0 — Baseline
- [x] `git status` limpo antes de começar
- [x] Build Debug de baseline (`src/` verde; 15 erros nos testes, esperados do doc 09)

### Fase 1 — Defeitos dos documentos 01–07
- [x] Diff programático dos 81 blocos de produção contra o código real
- [x] Registar `IClientProgressSummaryQueries` na DI da Infrastructure
- [x] Corrigir `LocalDates.ToLocalDate` (`ConvertTimeToUtc` → `ConvertTimeFromUtc`)
- [x] Remover `using Microsoft.VisualBasic` e `using System.Runtime.CompilerServices`
- [x] Renomear `trainingPlanId` → `trainingPlanDayId` em `GetMyCompletionAsync`
- [x] Build `src/` verde (0 erros, 0 avisos)

### Fase 2 — Índices EF e migration
- [x] Três índices parciais nas configurações (`foods`, `exercises`, `checkins`)
- [x] `dotnet ef migrations add AddModerationAndCheckInReadIndexes`
- [x] Conferir `Up`/`Down` contra o bloco de referência do doc 08 (3 CreateIndex / 3 DropIndex)
- [x] Normalizar ficheiros gerados para UTF-8 sem BOM e LF
- [x] `has-pending-model-changes` sem alterações pendentes
- [x] `dotnet ef database update` na base dev
- [x] Confirmar os três índices por `pg_indexes`

### Fase 3 — Testes do documento 09
- [x] 27 ficheiros criados
- [x] 4 substituídos integralmente
- [x] 12 excertos fundidos
- [x] Remover duplicado de validators na DI da Application
- [x] Corrigir `FakeClientQueries` (`NotImplementedException`)
- [x] Corrigir 6 defeitos nos próprios blueprints do doc 09
- [x] Build da solução verde (0 erros, 0 avisos)

### Fase 4 — Suite e contrato
- [x] `dotnet test PTManager.sln -c Release` → 2855 aprovados, 0 falhas, 1 skip
- [x] Snapshot regenerado: 162 → 170
- [x] Diff conferido: 8 novas, 3 alteradas, 0 removidas
- [x] Suite re-corrida com o snapshot novo, verde

### Fase 5 — Segurança e mutação
- [x] 8 invariantes do doc 00 verificadas contra o código real
- [x] M1–M6 aplicadas ao código real e todas mortas
- [x] Sem resíduos de mutação no working tree

### Fase 6 — QA e documentação
- [x] API arrancada contra a base dev; 8 rotas + 5 filtros respondem 401 (registados e protegidos)
- [x] 14 passos de QA do doc 10 §2 mapeados a testes funcionais automáticos
- [x] `sprint_6B/14_relatorio_implementacao_testes_fecho_6B.md`
- [x] `backlogs/QualityGates.md` — todos os `QG6B-*` fechados
- [x] `.claude/memory/ACTIVE.md`
- [x] `.claude/memory/Sessions/2026-09-20-sprint6b-fecho-implementacao.md`
- [x] `.claude/tasks/lessons.md`

**Sprint 6B: Finalizado.**

---

## Review

### O que se entregou

A 6B estava aplicada a meio: os documentos 01–07 no código, os 08 e 09 por fazer, e o build
dos testes partido. Ficou fechada com build Release limpo, 2855 testes verdes, migration de
três índices parciais aplicada à base dev, contrato HTTP de 162 para 170 operações e as seis
mutações dirigidas reconfirmadas mortas contra o código real — não contra a materialização
histórica.

### O que se encontrou pelo caminho

Oito defeitos, dos quais um crítico. O `LocalDates.ToLocalDate` convertia no sentido errado
e, por causa da validação de `Kind` do overload, **lançava exceção** em vez de devolver uma
data errada. Como é o único ponto de conversão do projeto, derrubava os cinco endpoints 6B
que dependem do dia local. Nenhum teste existia para o apanhar porque o documento 09 ainda
não tinha sido aplicado.

Os outros: uma dependência por registar que rebentaria o resumo do cliente em runtime, dois
validators registados em duplicado, dois `using` mortos deixados por autocomplete, um
parâmetro com nome enganador, um test double com `NotImplementedException`, e seis blocos do
próprio documento 09 que não compilavam.

### O que correu menos bem

Diagnosticar o 500 do `NextCheckIn_WithoutScheduledCheckIn` demorou mais do que devia: dois
`Guid` trocados numa chamada não dão erro de compilação, e o `dotnet test` não mostrava o
stack trace do servidor. Foi preciso montar um probe temporário com um `ILoggerProvider`.
A asserção do teste passou a incluir o corpo da resposta, para que a próxima vez seja
imediata.

### O que não foi feito

O QA manual com o seed de desenvolvimento do utilizador. Os catorze comportamentos estão
provados por testes funcionais contra PostgreSQL real através da pilha HTTP completa, e as
rotas foram sondadas contra a base dev a responder 401. O que falta é a inspeção visual dos
números concretos do seed dele — decisão dele, não bloqueio.

Sem commit, por decisão tomada no início da sessão.
