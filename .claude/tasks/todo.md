# Sprint 5 — Fase 5D: upload técnico de vídeo privado

Plano geral: `docs/backend-files/sprint_5/Plan_sprint_5.md` (Fase 5D).
Blueprints: `docs/backend-files/sprint_5/sprint_5D/`.
Materialização: `C:\ptm5d` (cópia de `backend/`, fora do repositório — `backend/src`,
`backend/tests` e as migrations reais permanecem intocados).

## Decisões fechadas com o utilizador (2026-09-13)

1. Storage: Cloudflare R2 (presigned PUT/GET com expiração real, egress grátis).
   Cloudinary recusado para vídeo: sem URL de leitura expirável no Free,
   `resource_type` fora da assinatura, créditos partilhados com imagens.
2. Formatos: container MP4 ou MOV; vídeo H.264 (`avc1`/`avc3`); áudio AAC (`mp4a`)
   ou ausente. Máx 100 MB, 3 min, lado maior ≤ 1920 px, lado menor ≥ 240 px.
   HEVC recusado (reprodução não garantida em todos os browsers).
3. Modelo: tabela própria `exercise_videos`; um vídeo ativo por exercício;
   substituição só troca quando o novo fica `Ready`; `exercises.video_url` externo
   intacto (contrato Preserve).
4. Quota: exercícios globais (superuser) sem quota — só limites por ficheiro e rate
   limit. Trainer: 20 vídeos (`Pending`+`Processing`+`Ready`), igual para todos,
   configurável, verificação atómica no store.
5. Leitura (URL assinada): cliente — exercícios presentes no seu plano de treino;
   trainer — globais e os seus privados; superuser — todos.
   Upload: superuser para globais, trainer para os seus privados.
6. TTL: presigned PUT 15 min; finalização até ao fim dessa janela; presigned GET
   30 min. Abandono: job agendado por upload (`expires_at` + margem) apaga o objeto e
   marca `Failed`; `Rejected`/`Failed`/substituídos apagados por outbox; lifecycle R2
   no prefixo de pendentes como rede de segurança.
7. Âmbito: só backend, `R2:Enabled=false` até existir bucket, CORS e secrets.
   `QG5-FRONTEND-001` continua aberto. Moderação automática de vídeo fora (DEF-TRUST-002).

## Decisões de arquitetura aprovadas (2026-09-13, 2.ª ronda)

8. Jobs de vídeo global (tenant nulo): marker `IPlatformDurableJobHandler`; só os
   handlers marcados (allowlist fechada) aceitam `TrainerId` nulo e correm com
   `TenantOrigin.System`. Restantes jobs mantêm a recusa.
9. Eliminação de objetos R2 (remover, substituído, rejeitado, abandonado): durable job
   `exercise-video.delete-object` na mesma transação da mutação; outbox intacta.
10. Eliminar exercício global com vídeo: 409 `global_exercise_has_video` (FK RESTRICT).
11. Superuser a ler vídeo privado: sem auditoria administrativa, só log estruturado.
    Escritas globais do superuser gravam `AdministrativeAuditEntry`.

## Factos da pesquisa R2 (fontes no relatório da sessão)

- `AWSSDK.S3` 4.0.103.2 / `AWSSDK.Core` 4.0.102.5, Apache-2.0, target net8.0.
- Config: `ServiceURL=https://<account>.r2.cloudflarestorage.com`, `AuthenticationRegion=auto`,
  `ForcePathStyle=true`, checksums `WHEN_REQUIRED`.
- Presign é cálculo local; R2 aceita 1 s–7 dias; não funciona com custom domain.
- Content-Type assinado é imposto pelo R2; Content-Length assinado NÃO garantido →
  `HeadObject` na finalização é a autoridade do tamanho.
- CORS do bucket obrigatório (PUT, GET, HEAD; `Content-Type`; expõe `ETag`).
- Lifecycle por prefixo remove em até 24 h → só rede de segurança.
- Class B (Head/GetObject) conta operações; DeleteObject gratuito.

## Factos verificados que condicionam o desenho

- `JobTenantValidator` recusa `TrainerId` nulo → vídeos globais exigem caminho de job
  de plataforma explícito e fechado (não genérico).
- Portal do cliente não expõe `video_url` hoje.
- Não há cron interno; jobs com `ScheduledAt` futuro são reclamados na ativação QStash.
- Media 5C é só imagem (rotas `image/*`, Skia, 6 MiB) — vídeo é slice separado.
- Testes de allowlist fechada (`JobDispatchArchitectureTests`,
  `JobDispatchCompositionTests`) têm de ser atualizados.

## 0. Preparação

- [x] Pesquisa R2 + AWSSDK.S3 (presign, HeadObject, Range, CORS, checksums)
- [x] Pesquisa layout ISO BMFF (parser MP4/MOV próprio)
- [x] Mapa: leitura do plano pelo cliente, leitura administrativa, tenant System
- [x] Cópia do backend para `C:\ptm5d` sem `bin`/`obj`
- [x] `dotnet restore --locked-mode` e build Release de baseline verdes na cópia

## 1. Desenho

- [x] Arquitetura (portas, entidade, estados, jobs, rotas, migration)
- [x] Documento 00 com decisões, ordem, invariantes, budget de I/O e gates

## 2. Materialização e validação na cópia

- [x] Domain, Application, Infrastructure, Api, migration, testes
- [x] Restore, build Release/Debug sem warnings, format, suites, has-pending-model-changes (não reexecutados nesta revisão)
- [x] Ciclo da migration no filtro de testes (62 Integration, inclui AddExerciseVideos)
- [x] Snapshot OpenAPI

## 3. Blueprints (extraídos da cópia)

- [x] Documentos 01..N por camada com blocos integrais (00–13)
- [x] QA da fase e rastreabilidade
- [x] `backlogs/QualityGates.md` com gates `QG5D-*`

## 4. Fecho

- [x] `git status`: nenhum ficheiro real de backend alterado
- [x] Memória (ACTIVE.md, MEMORY.md, nota de sessão)

---

# Correções da auditoria de segurança 2026-09-14 (patches 1–7)

Plano: `C:\Users\Leandro Alves\.claude\plans\stateless-moseying-truffle.md`.
Relatório: `backlogs/Security_and_Review_audit/11_relatorio_implementacao_patches.md`.

- [x] Patch 1 — PTM-SEC-18: `User.ResetAccessFailedCount` preserva `LockoutEnd`
- [x] Patch 2 — PTM-SEC-02: perfis 4096 px / 12 MP + semáforo de decode (sem rácio bytes/píxel)
- [x] Patch 3 — PTM-SEC-01: SDK 10.0.401 + `global.json` + Dockerfile/.dockerignore
- [x] Patch 4 — PTM-SEC-03: `ForwardedHeaders.None` sem proxies confiáveis
- [x] Patch 5 — PTM-SEC-04: lockout no change-password e no link Google (email antes da password)
- [x] Patch 6 — PTM-SEC-05: `IOutboxMessageHandler.RequiresActiveSubscription`
- [x] Patch 7 — PTM-SEC-06: teto `maxAttempts` no claim (outbox + durable) + docs 07/10 da 5D
- [x] Build Release + suite completa (real: 5D sem migration → worktree isolado: 2372 ✓ · 1 ✗ ambiental · 1 skip)
- [x] Relatório 11 + nota de sessão + memória
