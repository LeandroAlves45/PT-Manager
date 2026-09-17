# Estado ativo: Sprint 6A com blueprints validados (por implementar)

Atualizado: 2026-09-17 (blueprints da 6A validados — ver "Passo imediato")

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
