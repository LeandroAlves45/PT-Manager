# Estado ativo: Sprint 5D implementada e validada no backend

Atualizado: 2026-09-16

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
Transversal: `QG5-FRONTEND-001`.

## Passo imediato

1. O utilizador revê e faz commit das alterações 5D.
2. O Gate 5D do código está fechado, por isso segue-se a Sprint 6 (Frontend, 6A
   Fundações), conforme a decisão de 2026-09-15.
3. Não ativar `R2:Enabled` sem bucket, CORS e User Secrets.
4. Não editar a migration `20260916115919_AddExerciseVideos`, que já foi aplicada.
