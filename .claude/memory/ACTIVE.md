# Estado ativo: Sprint 5D planeada; implementação no backend real pendente

Atualizado: 2026-09-13

## Estado em uma linha

A Fase 5C está fechada no backend (falta `QG5C-PROVIDER-001`). A Fase 5D (upload
técnico de vídeo privado, Cloudflare R2) tem pack documental 00–13, Sprint Pack
e quatro defeitos da primeira extração corrigidos em `C:\ptm5d`. O `backend/`
real não foi alterado.

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

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/project/sprints/sprint-5/fase-5d/README.md` e `surface.yaml`.
3. `.claude/memory/Sessions/2026-09-13-sprint5d-planeamento-blueprints.md`.
4. `docs/backend-files/sprint_5/sprint_5D/00_desenho_aprovado_indice_dependencias_gates.md`.
5. `docs/backend-files/sprint_5/sprint_5D/11_qa_da_fase.md`.
6. `docs/backend-files/sprint_5/sprint_5D/12_rastreabilidade_revisao_quality_gates.md`.
7. `docs/backend-files/sprint_5/sprint_5D/13_relatorio_validacao_materializacao.md`.

## Gates abertos

Da 5D: `QG5D-JOB-001`, `QG5D-TEST-003`, `QG5D-OPENAPI-001`, `QG5D-ROLL-001`,
`QG5D-PROVIDER-001`. Persistência/probe/migration filtradas fecharam na cópia
(62 testes). Ver documentos 12 e 13.
Da 5C: `QG5C-PROVIDER-001`. Da 5B: `QG5B-STRIPE-001`, `QG5B-DEPLOY-001`.
Transversal: `QG5-FRONTEND-001`.

## Passo imediato

Implementar no `backend/` real pela ordem do documento 00, copiando os blocos
já corrigidos. Não recriar `C:\ptm5d`. Não activar `R2:Enabled` sem bucket,
CORS e User Secrets.
