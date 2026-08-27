# PT Manager: memória operacional

O código e os documentos em `.claude/project/` são as fontes de verdade. Esta
memória é um índice operacional conciso; detalhes e evidência permanecem nas
notas de `.claude/memory/Sessions/`.

## Estado atual

- Backend alvo em .NET 10 e C# 14, com Domain, Application, Infrastructure e
  Api. Arquitetura modular monolith e Clean Architecture, sem MediatR,
  AutoMapper, repositório genérico ou Unit of Work genérico.
- PostgreSQL com EF Core é a fonte de verdade. As migrations anteriores
  `20260804163659_InitialCreate`, `20260814121132_CompleteTrainingPhase2C` e
  `20260822155532_CompleteSprint3Phase3` são imutáveis. A migration
  `20260826172025_CompleteSprint3Lote3G` foi validada apenas em PostgreSQL 17
  descartável e ainda não foi aplicada a uma base persistente.
- Ainda não existe base de dados de produção identificada. Migrations e testes
  de schema referem desenvolvimento local ou PostgreSQL efémero em
  Testcontainers.
- `backend-python/` é apenas referência funcional e não define a arquitetura
  de destino.
- Sprint 2 e Sprint 3 estão concluídos. O Lote 3G fechou Authentication,
  Billing SaaS, Notifications e a relação ativa de clientes, incluindo a
  migration consolidada e validação PostgreSQL.

## Execução em curso

1. Lote 3F concluído e validado em PostgreSQL 17 descartável.
2. `20260822155532_CompleteSprint3Phase3` contém preflight, preservação de
   arquivo, backfills e índices trigram comprovados por medição.
3. Migrate, rollback e migrate passaram; nenhuma base persistente foi alterada.
4. Fixtures usam `MigrateAsync` e não suprimem pending model changes.
5. Suite final: 1085 testes aprovados, com 365 Domain, 365 Application, 331
   Infrastructure e 24 Architecture; build Release sem warnings.
6. `pg_trgm` foi aprovado com reduções medianas entre 97,7% e 98,3% nas três
   pesquisas seletivas medidas. O rollback remove os índices, mas mantém a
   extensão instalada por segurança.
7. A ordenação de packs foi alinhada com o índice PostgreSQL, reduzindo o plano
   medido de 6,888 ms para 0,146 ms.
8. Manter `InitialCreate`, `CompleteTrainingPhase2C` e
   `CompleteSprint3Phase3` imutáveis.
9. Gates 3G-A e 3G-B: código revisto/corrigido e testes escritos (764 testes
   unit+architecture passam). Testes PostgreSQL não correm — migration
   adiada para depois de Auth+Billing bloqueia a fixture partilhada de
   integração inteira, não só o 3G-A. Detalhe em
   `Sessions/2026-08-24-lote-3g-revisao-testes-validacao-parcial.md` e
   `docs/backend-files/sprints_concluidos/sprint_3/lote_3G/lote_3G-A_B/09_gates_3ga_3gb_validacao_final.md`.
10. Gates 3G-C (Authentication) e 3G-D (Billing SaaS): reconstrução documental
    concluída em `docs/backend-files/lote_3G/lote_3G-C_D/`. Os documentos 02 a
    09 contêm 140 caminhos únicos com código integral. Commands, validators,
    DTOs, portas, StoreStatus, StoreResult e handlers estão separados; Auth e
    Billing usam stores e gateways por área coesa. A materialização descartável
    compilou com zero warnings e zero erros; 1154 testes tinham passado antes da
    separação estrutural final dos dez StoreStatus. O backend real permanece
    inalterado e a implementação continua pendente. JWT concreto fica no Sprint
    4 e o adapter Stripe no Sprint 5. Detalhe em
    `Sessions/2026-08-25-lote-3g-c-d-reconstrucao-documental-final.md`.
11. Lote 3G e Sprint 3 fechados pós-migration (2026-08-26). A migration
    `20260826172025_CompleteSprint3Lote3G` inclui Auth, Billing, relação ativa de
    clientes e `last_provider_state_observed_at`. Migrate, rollback e migrate
    passaram em PostgreSQL 17 descartável. A asserção de propriedades jsonb do
    outbox foi tornada independente de ordem e o teste histórico Phase3 passou
    a migrar para um alvo explícito. Suite final: 1228 testes aprovados (381
    Domain, 451 Application, 360 Infrastructure, 36 Architecture), build Release
    sem warnings, formatação limpa e modelo EF sem alterações pendentes.
    Evidência em `Sessions/2026-08-26-sprint3-lote3g-pos-migration.md` e
    `docs/backend-files/sprints_concluidos/sprint_3/lote_3G/lote_3G-C_D/13_lote_3g_fecho_pos_migration.md`.
12. Ajuste residual 3G-B concluído:
    `EnqueueNotificationCommandValidator.IsSensitiveName` normaliza nomes antes
    da comparação e rejeita `token_value`, `refreshToken` e `apiKey`. Os três
    testes falharam antes da correção e passaram depois. Application.UnitTests:
    378 aprovados; formatação aprovada. Migration e testes PostgreSQL inalterados.

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
- O Gate 3G-A aprovou substituir `uq_clients_user` por
  `uq_clients_user_active`, filtrado por `user_id IS NOT NULL AND is_active =
  true AND is_deleted = false`. Assim, uma conta pode conservar relações
  históricas em vários tenants, mas apenas uma relação ativa. Código e testes
  já implementados (2026-08-24); a migration EF Core fica deliberadamente
  adiada para uma migration única gerada depois de Auth+Billing.
- Adiar uma migration de schema bloqueia toda a suite partilhada de
  `Infrastructure.IntegrationTests`, não apenas os testes da feature que a
  motivou — `PostgresContainerFixture.InitializeAsync` falha a
  `MigrateAsync` com `PendingModelChangesWarning` assim que o modelo EF
  diverge de uma migration gerada. Confirmado em 2026-08-24 com o índice
  `uq_clients_user_active` pendente: 308 de 340 testes de integração
  falharam, incluindo testes sem relação com 3G-A/3G-B.
- Google Sign-In pertence ao Sprint 4: trainers podem entrar diretamente;
  clientes exigem convite válido; associação a conta existente é explícita;
  `sub` é a identidade externa; roles, tenant, JWT e refresh token continuam
  controlados pelo PT Manager. `PasswordHash` não muda no Lote 3F.
- A política aprovada para novas passwords locais usa mínimo de 8 e máximo de
  128 caracteres. ASP.NET Core Identity permanece a fonte autoritativa; os
  validators de registo, alteração e recuperação repetem estes limites para
  feedback antecipado. O login valida apenas presença e um limite defensivo.
  Domain e EF Core recebem apenas o hash e não validam a password em claro nem
  exigem alteração de schema ou migration por causa desta política.

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
- Client Active Relationship e Notifications (lote_3G, migration pendente):
  `Sessions/2026-08-24-lote-3g-revisao-testes-validacao-parcial.md`.
- Authentication e Billing SaaS (lote_3G-C/D, documentação reconstruída):
  `Sessions/2026-08-25-lote-3g-c-d-reconstrucao-documental-final.md`.

## Padrões documentais

- Pseudocódigo alargado:
  `Patterns/blueprints_pseudocodigo_por_ficheiro.md`.
- Código C# integral por ficheiro:
  `Patterns/blueprints_codigo_real_por_ficheiro.md`.
- `docs/` é documentação local deliberadamente ignorada pelo Git. Nunca usar
  `git add -f` para a versionar.
