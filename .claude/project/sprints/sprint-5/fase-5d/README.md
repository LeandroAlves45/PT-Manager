# Sprint Pack — Fase 5D (vídeo privado de exercício)

Estado: **planeada e documentada**. Implementação manual no `backend/` real ainda
não começou. Materialização temporária em `C:\ptm5d` (fora do Git).

## Scope

Upload técnico de vídeo privado: presigned PUT/GET no Cloudflare R2, tabela
`exercise_videos`, probe ISO BMFF sem ffmpeg, durable jobs de plataforma
(`process`, `expire`, `delete-object`). Sem frontend. Sem moderação automática
de conteúdo de vídeo (DEF-TRUST-002). `R2:Enabled=false` até secrets e CORS.

Não junta restos da 5C.

## Ordem de leitura

1. `.claude/memory/ACTIVE.md`
2. Este README + `surface.yaml`
3. `.claude/memory/Sessions/2026-09-13-sprint5d-planeamento-blueprints.md`
4. `docs/backend-files/sprint_5/sprint_5D/00_desenho_aprovado_indice_dependencias_gates.md`
5. Documento N da tarefa (01–13)
6. Código só nos paths de `surface.yaml` ou do pedido

## Ordem de implementação

Ver documento 00. Resumo: Domain → Application (02–04) → pacotes AWS →
Infrastructure (05–07) → Api (08) → migration (09) → testes (10) → snapshot
OpenAPI → QA (11–13).

## Gates

Prefixo `QG5D-*`. Lista e estado em
`docs/backend-files/sprint_5/sprint_5D/12_rastreabilidade_revisao_quality_gates.md`
e `backlogs/QualityGates.md`.

Abertos por recursos externos: `QG5D-ROLL-001`, `QG5D-PROVIDER-001`.
`QG5-FRONTEND-001` e `QG5C-PROVIDER-001` continuam de sprints anteriores.

## Blockers

- Implementar no `backend/` real só depois de copiar os blueprints na ordem do 00.
- Não recriar `C:\ptm5d`. Não editar migrations já aplicadas.
- Não activar `R2:Enabled` sem bucket, CORS, token S3 e User Secrets.

## Blueprints

`docs/backend-files/sprint_5/sprint_5D/` (local, gitignored).
Padrão: `.claude/memory/Patterns/blueprints_codigo_real_por_ficheiro.md`.
