# Estado ativo: Sprint 6B FECHADA no backend real

Atualizado: 2026-09-20

## Sprint 6B (2026-09-20) — leituras agregadas, FECHADA

Pack `docs/blueprints/backend-files/sprint_6/sprint_6B/` (00–14). Implementada, testada e validada no
`backend/` real:

- Build Release: 0 erros, 0 avisos.
- Suite integral Release: **2855 aprovados, 0 falhas, 1 skip** (`RegenerateSnapshot`, por
  desenho). Domain 522, Application 719, Architecture 91, Functional 674, Integration 849.
- Migration `20260920114705_AddModerationAndCheckInReadIndexes` gerada pelo EF, só três
  índices parciais, aplicada à base dev e confirmada por `pg_indexes`.
- Snapshot OpenAPI `docs/api/api-surface.v1.txt`: 162 → 170 (8 operações novas, 3 linhas de
  filtros alteradas, 0 remoções).
- Mutações M1–M6 do documento 11 reaplicadas ao código real: todas mortas.
- Todos os `QG6B-*` fechados, incluindo `QG6B-IMPL-001`.

- **Flakiness do rate limiter na suite funcional corrigida** (2026-09-20, pós-fecho): o
  `TestServer` não preenche `RemoteIpAddress`, todos os pedidos anónimos caíam na partição
  `ip:unknown` (60/min) e os testes `*_WithoutToken_ReturnsUnauthorized` davam 429. Resolvido
  com um IP por `HttpClient` no `ApiWebApplicationFactory`; produção intocada. 3 corridas
  seguidas a 674/674.

**Sem commit.** O trabalho fica no working tree para revisão do utilizador.

### Defeitos corrigidos nesta sessão

1. **`LocalDates.ToLocalDate` usava `ConvertTimeToUtc` em vez de `ConvertTimeFromUtc`** —
   crítico: como o método força `Kind=Utc`, lançava `ArgumentException` para qualquer fuso
   que não fosse `TimeZoneInfo.Utc`, derrubando todos os endpoints 6B dependentes de data.
2. `IClientProgressSummaryQueries` não estava registado na DI da Infrastructure —
   `GET /clients/{id}/summary` rebentava em runtime.
3. `BlockFoodCommandValidator` e `BlockExerciseCommandValidator` registados em duplicado
   (82 validators em vez de 80).
4. `using Microsoft.VisualBasic` e `using System.Runtime.CompilerServices` mortos.
5. `GetMyCompletionAsync` com o parâmetro chamado `trainingPlanId` quando é o id do **dia**.
6. `FakeClientQueries` com um overload `throw new NotImplementedException()` gerado pelo IDE.
7. Seis defeitos nos próprios blueprints do documento 09 (usings em falta, `Model` em vez de
   `_model`, `[Collection]` em falta, e `IssueClient(trainerId, clientUserId)` com os
   argumentos trocados — a assinatura é `IssueClient(clientUserId, trainerId)`).

Ver `Sessions/2026-09-20-sprint6b-fecho-implementacao.md` e
`sprint_6B/14_relatorio_implementacao_testes_fecho_6B.md`.

## Próximo passo

Sprint 6C: frontend. O backend das leituras agregadas está pronto.

## Estado em uma linha

A Fase 5D (vídeo privado de exercício, Cloudflare R2) está implementada no
`backend/` real, com a migration `20260916115919_AddExerciseVideos` aplicada à base dev
e 2644 testes verdes. Só faltam os gates externos do R2. As alterações desta sessão
ainda não têm commit (decisão do utilizador).

## Decisões da 5D

1. Storage: Cloudflare R2 (não Amazon S3). `AWSSDK.S3` é só o protocolo.
2. MP4/MOV, H.264 `avc1`/`avc3`, AAC `mp4a` ou sem áudio; 100 MiB; 3 min;
   lado maior ≤ 1920; lado menor ≥ 240. HEVC recusado.
3. Tabela `exercise_videos`; um Ready e um in-flight por exercício;
   `exercises.video_url` Preserve.
4. Quota trainer: 20 exercícios com vídeo Pending/Processing/Ready. Global sem quota.
5. Leitura: cliente = plano activo; trainer = globais + seus; superuser = todos.
6. PUT 15 min; GET 30 min; abandono = `UploadExpiresAt` + grace ≥ 1 h.
7. Só backend; `R2:Enabled=false`; `QG5-FRONTEND-001` aberto; moderação fora
   (DEF-TRUST-002).
8. Jobs globais: `IPlatformDurableJobHandler` + `TenantOrigin.System`.
9. Eliminação de objectos: durable job `exercise-video.delete-object`.
10. Delete de exercício global com vídeo: 409 `global_exercise_has_video`.
11. Superuser a ler privado: log estruturado, sem auditoria persistida.

## Correções da revisão

Content-Type ignora parâmetros; `complete` tem `video_upload`; deletes de
`ExerciseVideo` passam no interceptor (`Remove`, não `ExecuteDelete` no Ready
anterior); `AbandonmentGrace` mínima de 1 hora.

## Fecho 2026-09-16

Na revisão dos documentos 01–08 aplicados pelo utilizador foram corrigidos 13 defeitos
e 5 divergências. Entre os defeitos: codecs H.264 perdidos, verificação de duração
removida, interceptor sem `Deleted`, SQLSTATE errado no tradutor, guardas do
`MarkUploaded`, job types renomeados e marca `"M4V "`. Houve 10 testes de regressão,
validados por mutação. Detalhe em
`docs/backend-files/sprint_5/sprint_5D/14_relatorio_fecho_fase_5D.md`.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-16-sprint5d-fecho-implementacao.md`.
3. `docs/backend-files/sprint_5/sprint_5D/14_relatorio_fecho_fase_5D.md`.
4. `.claude/project/sprints/sprint-5/fase-5d/README.md`.
5. `docs/backend-files/sprint_5/sprint_5D/12_rastreabilidade_revisao_quality_gates.md`.

## Gates abertos

Da 5D: `QG5D-ROLL-001` e `QG5D-PROVIDER-001` (conta R2, bucket, CORS, token, User Secrets).
Da 5C: `QG5C-PROVIDER-001`. Da 5B: `QG5B-STRIPE-001` e `QG5B-DEPLOY-001`.
Transversal: `QG5-FRONTEND-001` (fecha na 6G).

## Passo imediato

**2026-09-17 (fim do dia) — Sprint 6A FECHADA no backend real.** Revisão 01–08 do utilizador
por diff programático (70 desvios, 1 defeito real: `MuscleGroupCatalog.TryNormalize(null)`
devolvia false → corrigido). Pack de testes aplicado + `QG6A-TEST-004` (4 testes) + teste do 2.º
ramo do preflight; 5 mutações mortas. Migration `20260917152023_AddSprint6AWriteSchema` gerada,
aplicada à dev. Suite 2760 verde, snapshot 154→162. Todos os `QG6A-*` fechados. Sem commit.
Próximo: commit (utilizador) e planear 6B. Relatório `sprint_6A/14_relatorio_implementacao_testes_fecho_6A.md`;
nota `Sessions/2026-09-17-sprint6a-fecho-implementacao.md`.

**2026-09-17 — Sprint 6A: blueprints de código real validados.** Pack
`docs/backend-files/sprint_6/sprint_6A/` (00–13), 155 alvos extraídos por script de uma
materialização descartável (worktree `C:\ptm6a`, removido, sem merge/commit): build 0/0,
2755 testes verdes (+111), 6 mutações mortas, snapshot +8 operações, migration
`AddSprint6AWriteSchema` (o timestamp muda ao gerar) com preflight no Down. Decisões D1–D8
no `00`. Próximo: o utilizador implementa pela ordem do `00`, aplica a migration à dev e
fecha `QG6A-IMPL-001`; depois planear a 6B (decisões 5–8 do relatório §8). Nota:
`Sessions/2026-09-17-sprint6a-blueprints-validados.md`.

### Histórico do passo anterior (2026-09-16)

1. O Gate 5D do código está fechado (commit `8e0f8f0`).
2. **Sprint 6 planeado em 2026-09-16 (backend-first):** 6A backend escrita/schema →
   6B backend leituras agregadas → 6C–6G frontend. Nenhuma fase iniciada.
   Entrada: `docs/backend-files/sprint_6/plan_sprint_6_por_atualizar.md`,
   `.claude/project/sprints/sprint-6/README.md` e
   `.claude/project/frontend/layout/01_RELATORIO_ANALISE.md`.
   Nota: `Sessions/2026-09-16-sprint6-plano-layout.md`.
3. Próximo pedido esperado: blueprint da 6A, fechando antes as decisões abertas
   (relatório §8).
4. Referência visual = PNG em `.claude/project/frontend/layout/assets/` (mapeamento no
   README do layout). Não abrir o Claude Design nem PDF. Falta só screenshot do 08 (⌘K).
5. Não ativar `R2:Enabled` sem bucket, CORS e User Secrets.
6. Não editar a migration `20260916115919_AddExerciseVideos`, que já foi aplicada.
