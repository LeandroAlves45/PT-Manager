# PT Manager: memória operacional

O código e os documentos em `.claude/project/` são as fontes de verdade. Esta
memória é um índice operacional conciso; detalhes e evidência permanecem nas
notas de `.claude/memory/Sessions/`.

## Estado atual

- Backend alvo em .NET 10 e C# 14, com Domain, Application, Infrastructure e
  Api. Arquitetura modular monolith e Clean Architecture, sem MediatR,
  AutoMapper, repositório genérico ou Unit of Work genérico.
- PostgreSQL com EF Core é a fonte de verdade. As migrations
  `20260804163659_InitialCreate` e `20260814121132_CompleteTrainingPhase2C`
  foram aplicadas em desenvolvimento e são imutáveis.
- Ainda não existe base de dados de produção identificada. Migrations e testes
  de schema referem desenvolvimento local ou PostgreSQL efémero em
  Testcontainers.
- `backend-python/` é apenas referência funcional e não define a arquitetura
  de destino.
- Sprint 2 está concluído. Sprint 3 está em execução; Clients, Nutrition,
  Training, Packs, Sessions, Assessments, Supplements, TrainerSettings,
  branding e administração global foram materializados até ao Lote 3E.

## Execução em curso

1. Lote 3E concluído e validado no backend real.
2. Não gerar migration intermédia.
3. No Lote 3F, consolidar a migration, medir os planos SQL relevantes com
   `EXPLAIN` e validar migrate, rollback e migrate.
4. Manter `CompleteTrainingPhase2C` imutável.

O gate final do Lote 3E aprovou build Release sem warnings, formatação e 1065
testes: 365 Domain, 365 Application, 311 integração PostgreSQL e 24 arquitetura.
Evidência em `Sessions/2026-08-22-sprint3-phase3-lot3e-completion.md`.

A autorização de `ExerciseSetLogs` ainda exige decisão explícita de ator e
ownership. `PreviewNutrition` é cálculo puro sem I/O. A revisão que substitui
alegações anteriores desatualizadas está em
`Sessions/2026-08-21-auditoria-blueprints-lote-3e-e-autorizacao.md`.

## Decisões duráveis

- Preservar `/api/v1`, JSON em `snake_case` e o campo `detail` em Problem
  Details. Classificar alterações de contrato como Preserve, Alias ou Remove.
- Usar `Result` e `Result<T>` para falhas esperadas. Controllers permanecem
  finos e os handlers propagam `CancellationToken`.
- O tenant vem exclusivamente de `ITenantContext`. Query filters protegem
  leituras; interceptor, stores, constraints e testes cross-tenant protegem
  escritas. `null` nunca concede acesso global.
- Superuser usa casos administrativos explícitos, autorização fail-closed e
  auditoria append-only. Trainers não alteram recursos privados de outro tenant.
- Moderação administrativa permite ao superuser bloquear ou desbloquear um
  `Food` ou `Exercise` privado apenas em casos restritos de maldade, perigo ou
  fraude deliberada. Não concede edição funcional, mudança de owner, hard
  delete nem listagem transversal implícita.
- `PlatformEnforcementStatus` é separado de `IsActive`. Conteúdo bloqueado sai
  imediatamente do portal do cliente, novas referências são rejeitadas e planos
  existentes derivam a necessidade de revisão sem perder a referência histórica.
  Estado e auditoria são atómicos.
- A implementação fica para o Sprint 4B e gera uma migration EF Core nova;
  não entra no Lote 3F. Decisão completa em
  `Sessions/2026-08-21-private-catalog-moderation-decision.md` e
  `.claude/project/00_ARCHITECTURE.md` §17.5.
- PostgreSQL é a fonte de verdade para jobs, outbox, autorização, billing e
  quotas de negócio. Redis é apenas cache reconstruível e rate limiting; QStash
  apenas ativa o dispatcher.
- Webhooks Stripe exigem raw body, assinatura, deduplicação, idempotência,
  reconciliação e outbox transacional.
- `TrainerSettings.LogoUrl` e `LogoPublicId` representam apenas media
  personalizado. Null instrui o frontend a usar o seu asset padrão; o backend
  não persiste uma URL global. Detalhe em
  `Sessions/2026-08-21-correcao-final-blueprints-lote-3e.md`.
- `IMediaStorage` é uma porta agnóstica ao tipo de media. O logótipo usa upload
  antes da transação, compensação síncrona se a persistência falhar e outbox
  transacional para eliminar o asset anterior.
- `Exercise.VideoUrl` permanece URL HTTPS externa no Lote 3E, sem download nem
  garantia de validação técnica ou de conteúdo.
- Upload privado de vídeo fica para o Sprint 5B, depois do Lote 3F, Sprint 4 e
  integração base do Cloudinary no Sprint 5A. Inclui ownership, integridade,
  inspeção real, estados técnicos, jobs, acesso assinado, rate limiting, quotas
  e testes cross-tenant.
- Moderação automática fica fora do Sprint 5B e do MVP atual. A futura porta da
  Application será independente do fornecedor e criada apenas com o primeiro
  consumidor real. Estado técnico e decisão de moderação permanecem separados.
  Decisão completa em
  `Sessions/2026-08-21-exercise-video-upload-decision.md` e
  `.claude/project/00_ARCHITECTURE.md` §17.4.
- `Food`, `Exercise` e `Supplement` usam disponibilidade por `IsActive`, sem
  soft delete. Referências históricas permanecem legíveis; novas referências
  rejeitam itens inativos ou invisíveis ao tenant.
- `CatalogReferenceLocking` usa `FOR SHARE` para fechar a corrida entre validar
  referências e arquivar administrativamente o mesmo item.
- Mutações globais de Food e Exercise usam `ExecuteInTransactionAsync` e
  confirmam commits ambíguos pelo ID único da auditoria atómica. Create preserva
  a identidade do aggregate entre retries.
- `uq_clients_user` continua único global. Permitir a mesma conta cliente com
  vários trainers requer decisão de produto e auth no Sprint 4.

## Regras funcionais com fonte canónica

- Nutrição, fórmulas, snapshots, macros, Food e constraints:
  `.claude/project/01_DATABASE_SCHEMA.md` e
  `Sessions/2026-08-12-sprint3-phase2-lot2a-completion.md`.
- Training e migration aplicada:
  `Sessions/2026-08-14-sprint3-phase2c-completion.md`.
- Packs:
  `Sessions/2026-08-15-sprint3-phase3-lot3a-completion.md`.
- Sessions:
  `Sessions/2026-08-17-sprint3-phase3-lot3b-completion.md`.
- Assessments:
  `Sessions/2026-08-18-sprint3-phase3-lot3c-completion.md`.
- Supplements e catálogo global:
  `Sessions/2026-08-19-sprint3-phase3-lot3d-completion.md`.
- TrainerSettings, branding e administração global:
  `Sessions/2026-08-22-sprint3-phase3-lot3e-completion.md`.

## Padrões documentais

- Pseudocódigo alargado:
  `Patterns/blueprints_pseudocodigo_por_ficheiro.md`.
- Código C# integral por ficheiro:
  `Patterns/blueprints_codigo_real_por_ficheiro.md`.
- `docs/` é documentação local deliberadamente ignorada pelo Git. Nunca usar
  `git add -f` para a versionar.
