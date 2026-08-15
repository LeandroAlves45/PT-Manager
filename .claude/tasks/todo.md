# Todo — Sprint 3: Application Layer (plano em 4 fases)

*Planeado em 2026-08-06. Estratégia híbrida aprovada: Fase 1 = fundações transversais + feature de referência; Fases 2-4 = grupos verticais de features completos. Cada fase produz files .md de pseudocódigo alargado (bloco único com usings completos, XML Docs, comentários explicativos, Nota de mentor + Validações no fim de cada ficheiro) em `docs/backend-files/sprint_3/fase_N/`. Código real só quando for pedido explicitamente.*

## Decisões aprovadas pelo utilizador (2026-08-06)

- Divisão **híbrida** (não horizontal): cada fase compila e testa sozinha.
- Repositórios deferidos do Sprint 2 entram **por feature**: porta na Application + implementação EF Core na Infrastructure, apenas para os casos de uso da fase (YAGNI).
- Testes unitários **dentro de cada fase**, não numa fase final.
- Authentication entra no Sprint 3 **contra portas** (IPasswordHasher, ITokenService, IUserRepository); implementações concretas (Identity/JWT) ficam para o Sprint 4.

## Regras transversais a todas as fases

- Handlers devolvem `Result`/`Result<T>` com Error (código estável + categoria: Validation, NotFound, Conflict, Unauthorized, Forbidden, PaymentRequired, ExternalDependency) — `00_ARCHITECTURE.md §4.3`.
- FluentValidation **core** (sem `.AspNetCore`), chamado explicitamente no handler via `validator.ValidateAsync`.
- Mapping manual por extensão (`ToDto()`), sem AutoMapper. DTOs junto do handler da feature.
- Tenant exclusivamente via `ITenantContext`; nunca aceitar `trainer_id` de payload.
- `CancellationToken` propagado em todas as operações de I/O.
- Application não depende de Infrastructure (verificado por ArchitectureTests).
- Preview nutricional não persiste; escritas recalculam no servidor.

## Fase 1 — Fundações + Clients (feature de referência)

- [x] Foundations: Result/Error, validação, paginação e tenant fail-closed
- [x] Clients: contratos, validators, handlers e mapping manual
- [x] Clients: portas Application, store composto e queries EF Core
- [x] Tradução PostgreSQL por operação e constraint conhecida
- [x] Testes unitários de Foundations e Clients
- [x] Testes PostgreSQL de queries, atomicidade, concorrência e tenant
- [x] Blueprints completos em `docs/backend-files/sprint_3/fase_1/`
- [x] Gate Fase 1: commit `aee7f6d`, revisão concluída e padrão congelado

## Fase 2 — Nutrition + Training

- [x] Lote 2A Nutrition: documentação e implementação de Domain, Application,
  Infrastructure e testes concluídas em 12/08/2026
- [x] Gate 2A: cálculo, snapshots, reconciliação, catálogos, concorrência e
  tenant revistos com 24 testes PostgreSQL específicos
- [x] Lote 2B Training: Exercise, TrainingPlan, estrutura e logs históricos
- [x] Gate 2B: edição segura, replacement, locks, concorrência e tenant revistos
- [x] Lote 2C: configurações EF Core e migration
  `20260814121132_CompleteTrainingPhase2C` concluídas em 14/08/2026
- [x] Gate Fase 2: revisão final aprovada no commit `3441421`, com 652 testes
  distintos aprovados e modelo EF Core sem alterações pendentes

## Fase 3 — Sessions + Assessments + Supplements + TrainerSettings

- [x] Desenho funcional da Fase 3 aprovado pelo utilizador em 14/08/2026
- [x] `fase_3/00_plano_fase_3.md`: decisões, dependências, schema e gates
- [x] Blueprints Lote 3A: PackType e ClientSessionPack concluídos e autoavaliados
- [x] Implementação real do Lote 3A concluída em 15/08/2026
- [x] Gate 3A: snapshots, packs simultâneos, cancelamento seguro e data apenas
  informativa. PostgreSQL diferido explicitamente para a migration consolidada 3F
- [ ] Lote 3B: Sessions, agenda, estados, packs, locks e idempotência
- [ ] Gate 3B: concorrência da última sessão, sobreposições, timezone e rollback
- [ ] Lote 3C: InitialAssessment e CheckIn
- [ ] Gate 3C: unicidade, correção e ausência de efeitos laterais em Nutrition
- [ ] Lote 3D: Supplement e ClientSupplementAssignment
- [ ] Gate 3D: catálogos globais/privados, atribuições e tenant
- [ ] Lote 3E: TrainerSettings, media e administração de catálogos globais
- [ ] Gate 3E: branding opcional, timezone, outbox de media e autorização administrativa
- [ ] Lote 3F: configurações EF Core, migration gerada e gate transversal
- [ ] Gate Fase 3: revisão do utilizador

## Fase 4 — Authentication + Billing + Notifications + gate final do sprint

- [ ] 00_plano_fase_4.md
- [ ] Authentication: Login/Signup/RefreshToken handlers contra portas (IPasswordHasher, ITokenService, IUserRepository, InviteToken flow) — sem JWT/Identity concretos
- [ ] Billing: CreateCheckoutSessionHandler, ProcessStripeWebhookHandler (dedupe `processed_stripe_events` + outbox na mesma transação), packs + snapshots de packs
- [ ] Notifications: EnqueueNotificationHandler (via outbox/durable jobs)
- [ ] Gate final Sprint 3: ~60+ testes Application verdes, build Release sem warnings, `dotnet format`, Application sem dependência de Infrastructure, docs atualizados
- [ ] Marcar "Finalizado" na checklist do sprint

## Skills a usar na execução

- `graphify-pseudocode` — geração dos files .md de pseudocódigo
- `ponytail-ptmanager` — guarda YAGNI em todos os handlers
- `testing` — desenho dos testes unitários por fase
- `plan-code-builder` — quando for pedido código real
- `obsidian-ptmanager` — memória no fim de cada sessão

## Review

*(a preencher no fim de cada fase)*
