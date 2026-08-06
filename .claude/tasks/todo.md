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

- [ ] 00_plano_fase_1.md — overview, ordem de leitura, checklist da fase
- [ ] 01_packages_result_error.md — FluentValidation em `Directory.Packages.props`; `Application/Common/Results/` (Result, Result<T>, Error, ErrorCategory)
- [ ] 02_exceptions.md — `Application/Common/Exceptions/` (ValidationException, ExternalServiceException; fronteira exceção vs Result)
- [ ] 03_clients_dtos_validators.md — DTOs + validators de Clients (Create/Update/Archive/Get/List)
- [ ] 04_clients_handlers_porta.md — handlers de Clients + porta `IClientsRepository` (Application)
- [ ] 05_clients_repository_infra.md — `ClientsRepository` EF Core (Infrastructure)
- [ ] 06_clients_mapping_tests.md — extensões de mapping + testes unitários (handlers com mocks, validators)
- [ ] Gate Fase 1: revisão do utilizador; padrão de referência congelado

## Fase 2 — Nutrition + Training

- [ ] 00_plano_fase_2.md
- [ ] Nutrition: DTOs/validators/handlers (MealPlan CRUD + preview de cálculo sem persistência, snapshot imutável derivado no servidor, Food) + porta + repository + testes
- [ ] Training: DTOs/validators/handlers (TrainingPlan, dias, exercícios, sets, logs de cliente) + porta + repository + testes
- [ ] Gate Fase 2: revisão do utilizador

## Fase 3 — Sessions + Assessments + Supplements + TrainerSettings

- [ ] 00_plano_fase_3.md
- [ ] Sessions: estados de sessão, `starts_at`, interação com packs (deferido do Sprint 2) + porta + repository + testes
- [ ] Assessments: InitialAssessment + CheckIn (CheckIn nunca altera dieta) + testes
- [ ] Supplements: Supplement + ClientSupplementAssignment + testes
- [ ] TrainerSettings: handlers mínimos + testes
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
