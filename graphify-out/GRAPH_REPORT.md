# Graph Report - .  (2026-08-08)

## Corpus Check
- 96 files · ~144,566 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 4236 nodes · 7990 edges · 323 communities (265 shown, 58 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 325 edges (avg confidence: 0.8)
- Token cost: 54,354 input · 0 output

## Community Hubs (Navigation)
- Client Store Tests
- Durable Job Repository Tests
- Auth Context test / card
- Initial Assessment Tests
- command
- supplement Api
- Durable Job Tests
- Client Handlers Tests
- Client Supplement Assignment
- admin Api
- Client Tests
- Outbox Message Tests
- Infrastructure Integration Tests / Archi
- Dependency Injection / IClock
- Archive Client Command / Archive Client
- clients Api / pack Types Api
- Result And Error Tests
- training Plan
- packages lock
- Initial Create Migration Tests
- packages lock
- Pt Manager Db Context
- Client Test Doubles
- Energy Requirement Calculator
- validators
- packages lock
- packages lock
- helpers
- Client Store Tests
- packages lock
- Catalog Reference Validation Tests
- packages lock
- Pagination Validation Rules Tests
- Trainer Subscription
- Macro Target Calculator Tests
- packages lock
- nutrition Api
- packages lock
- Durable Job / Durable Job Configuration
- packages lock
- assessments Api
- tsconfig
- Postgres Constraint Translator Tests
- Meal Plan Configuration / Meal Plan Meal
- Postgres Container Fixture
- packages lock
- Notification Configuration
- packages lock
- packages lock
- tsconfig app
- Subscription Status Tests
- Notification Tests
- packages lock
- packages lock
- packages lock
- Client Store
- IClient Queries / Client Details Dto
- IClient Store
- Session
- packages lock
- packages lock
- packages lock
- packages lock
- Processed Stripe Event Configuration / T
- Food
- Meal Plan
- Tenant Write Validation Interceptor
- Client Supplement Assignment Tests
- package
- packages lock
- packages lock
- packages lock
- packages lock
- Pack Type
- User
- Meal Plan Meal
- Trainer Settings
- packages lock
- packages lock
- tsconfig node
- packages lock
- Invite Token Tests
- Refresh Token Configuration
- packages lock
- packages lock
- launch Settings
- Job Status Tests
- 20260804163659 Initial Create
- packages lock
- packages lock
- Tenant Context Extensions Tests
- Exercise
- packages lock
- packages lock
- Layer Dependency Tests
- packages lock
- Training Plan
- packages lock
- Persistence Schema Metadata Tests
- packages lock
- packages lock
- Round Trip And Constraint Tests
- packages lock
- packages lock
- Meal Plan Tests
- packages lock
- chart
- Exercise Set Tests
- packages lock
- packages lock
- packages lock
- packages lock
- package
- carousel
- Client Session Pack
- Refresh Token
- Food Macro Invariant Tests
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Training Plan Day Exercise Tests
- packages lock
- packages lock
- Check In Configuration
- Nutrition Model Metadata Tests
- packages lock
- packages lock
- packages lock
- packages lock
- Meal Plan Calculation Tests
- Session Tests
- packages lock
- packages lock
- Client Mappings
- Client Exercise Set Log
- Nutrition Calculation Snapshot
- System Clock
- packages lock
- packages lock
- packages lock
- packages lock
- Tenant Write Validation Interceptor Test
- packages lock
- packages lock
- packages lock
- package
- context-menu
- Exercise Set
- packages lock
- Tenant Query Filter Tests
- packages lock
- Food Macro Invariant Tests
- packages lock
- handlers
- packages lock
- Validation Result Extension Tests
- Training Plan Day Configuration
- Training Plan Day Exercise Configuration
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Client Session Pack Tests
- packages lock
- Email Address Tests
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Catalog Reference Validation Tests
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- compose
- Pagination Validation Rules
- Training Plan Configuration
- Test Tenant Context
- Client Exercise Set Log Tests
- Training Adjustments Tests
- Check In Feedback Tests
- Authcontext
- packages lock
- packages lock
- packages lock
- packages lock
- Jobs Model Metadata Tests
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Nutrition Adjustments Tests
- packages lock
- packages lock
- Macro Summary Tests
- package
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Macro Distribution Mode
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- packages lock
- Pt Manager Db Context
- ci
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- package
- vercel
- vitest config
- IRead Only Collection
- List
- Meal Plan Meal
- Meal Plan Meal Item
- Meal Plan Meal Supplement
- Notification
- Processed Stripe Event
- Refresh Token
- Cancellation Token
- Test Tenant Seed
- IClass Fixture
- Model Builder
- Postgre Sql Container

## God Nodes (most connected - your core abstractions)
1. `Domain.ValueObjects` - 64 edges
2. `cn()` - 61 edges
3. `net10.0` - 54 edges
4. `PtManagerDbContext` - 49 edges
5. `net10.0` - 45 edges
6. `net10.0` - 45 edges
7. `Button` - 39 edges
8. `ClientHandlersTests` - 34 edges
9. `net10.0` - 33 edges
10. `ClientStoreTests` - 30 edges

## Surprising Connections (you probably didn't know these)
- `ptmanager-local Docker Compose Stack` --conceptually_related_to--> `Frontend index.html (Vite entry)`  [AMBIGUOUS]
  backend/compose.yml → frontend/index.html
- `AlertsPanel()` --indirect_call--> `Calendar()`  [INFERRED]
  frontend/src/components/dashboard/AlertsPanel.jsx → frontend/src/components/ui/calendar.tsx
- `ContextMenuShortcut()` --calls--> `cn()`  [EXTRACTED]
  frontend/src/components/ui/context-menu.tsx → frontend/src/lib/utils.ts
- `ClientErrors` --references--> `Error`  [EXTRACTED]
  backend/src/Application/Features/Clients/ClientErrors.cs → backend/src/Application/Errors/Error.cs
- `CI Placeholder Workflow` --semantically_similar_to--> `CD Placeholder Workflow`  [INFERRED] [semantically similar]
  .github/workflows/ci.yml → .github/workflows/cd.yml

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Local Development Stack (Backend DB + Frontend Entry + CI/CD Placeholders)** — backend_compose_ptmanager_local, frontend_index_html, github_workflows_ci_placeholder, github_workflows_cd_placeholder [INFERRED 0.65]

## Communities (323 total, 58 thin omitted)

### Community 0 - "Client Store Tests"
Cohesion: 0.05
Nodes (57): Action, ActivitySeed, CancellationToken, DateOnly, Guid, IReadOnlyList, string, Task (+49 more)

### Community 1 - "Durable Job Repository Tests"
Cohesion: 0.07
Nodes (45): CancellationToken, DateTime, DurableJob, Guid, IClock, IReadOnlyList, Task, TimeSpan (+37 more)

### Community 2 - "Auth Context test / card"
Cohesion: 0.06
Nodes (58): activateTrainer(), getPlatformMetrics(), getTrainers(), grantExemption(), revokeExemption(), suspendTrainer(), createBillingPortal(), createCheckout() (+50 more)

### Community 3 - "Initial Assessment Tests"
Cohesion: 0.05
Nodes (27): DateOnly, DateTime, Guid, CheckIn, DateTime, Guid, InitialAssessment, ActivityLevel (+19 more)

### Community 4 - "command"
Cohesion: 0.05
Nodes (53): bottomNavItems, isItemActive(), NavContent(), navItems, Alert, AlertDescription, AlertTitle, alertVariants (+45 more)

### Community 5 - "supplement Api"
Cohesion: 0.11
Nodes (40): archiveSupplement(), createSupplement(), deleteSupplement(), unarchiveSupplement(), updateSupplement(), MUSCLE_GROUPS, MuscleMultiSelect(), getStatusStyle() (+32 more)

### Community 6 - "Durable Job Tests"
Cohesion: 0.08
Nodes (21): IDurableJobStore, CancellationToken, DateTime, Guid, IReadOnlyList, Task, TimeSpan, DateTime (+13 more)

### Community 7 - "Client Handlers Tests"
Cohesion: 0.12
Nodes (13): ReactivateClientCommand, CancellationToken, Task, ClientHandlersTests, Fact, Guid, InlineData, Task (+5 more)

### Community 8 - "Client Supplement Assignment"
Cohesion: 0.06
Nodes (24): DateTime, Guid, ClientSupplementAssignment, DateTime, Guid, Supplement, EntityTypeBuilder, ClientSupplementAssignmentConfiguration (+16 more)

### Community 9 - "admin Api"
Cohesion: 0.13
Nodes (36): archiveGlobalSupplement(), createGlobalExercise(), createGlobalFood(), createGlobalSupplement(), deleteGlobalExercise(), deleteGlobalFood(), deleteGlobalSupplement(), getGlobalExercises() (+28 more)

### Community 10 - "Client Tests"
Cohesion: 0.08
Nodes (18): DateOnly, DateTime, Guid, Client, BiologicalSex, DateOnly, BirthDate, EntityTypeBuilder (+10 more)

### Community 11 - "Outbox Message Tests"
Cohesion: 0.09
Nodes (18): IOutboxStore, CancellationToken, DateTime, Guid, IReadOnlyList, Task, TimeSpan, DateTime (+10 more)

### Community 12 - "Infrastructure Integration Tests / Archi"
Cohesion: 0.05
Nodes (35): Microsoft.NET.Sdk, Microsoft.NET.Sdk, coverlet.collector, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk, coverlet.collector (+27 more)

### Community 13 - "Dependency Injection / IClock"
Cohesion: 0.06
Nodes (32): IClock, DateTime, ITenantContext, TenantOrigin, Guid, ITenantContextInitializer, Guid, TenantOrigin (+24 more)

### Community 14 - "Archive Client Command / Archive Client"
Cohesion: 0.07
Nodes (23): ArchiveClientCommand, CreateClientHandler, IClock, ITenantContext, IValidator, ListClientsHandler, ITenantContext, IValidator (+15 more)

### Community 15 - "clients Api / pack Types Api"
Cohesion: 0.10
Nodes (25): createUser(), signupTrainer(), api, failedQueue, archiveClient(), createClient(), unarchiveClient(), updateClient() (+17 more)

### Community 16 - "Result And Error Tests"
Cohesion: 0.09
Nodes (14): CommonErrors, Error, IReadOnlyList, ErrorCategory, ValidationError, CancellationToken, Task, Result (+6 more)

### Community 17 - "training Plan"
Cohesion: 0.11
Nodes (24): addExerciseToDay(), createPlanDay(), createTrainingPlan(), deleteDayExercise(), deletePlanDay(), deleteTrainingPlan(), getDayExercises(), getPlanDays() (+16 more)

### Community 18 - "packages lock"
Cohesion: 0.05
Nodes (37): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+29 more)

### Community 19 - "Initial Create Migration Tests"
Cohesion: 0.10
Nodes (19): Fact, InlineData, string, Task, Theory, ValueTask, InitialCreateMigrationTests, string (+11 more)

### Community 20 - "packages lock"
Cohesion: 0.06
Nodes (36): Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Primitives, Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Primitives, contentHash, dependencies (+28 more)

### Community 21 - "Pt Manager Db Context"
Cohesion: 0.06
Nodes (33): CheckIn, Client, ClientSessionPack, ClientSupplementAssignment, DurableJob, Exercise, Food, Guid (+25 more)

### Community 22 - "Client Test Doubles"
Cohesion: 0.15
Nodes (19): ArchiveClientStoreOutcome, SaveClientProfileOutcome, ClientSummaryDto, UsableClientPackDto, ClientActivityFilter, CancellationToken, Task, PageRequest (+11 more)

### Community 23 - "Energy Requirement Calculator"
Cohesion: 0.14
Nodes (9): EnergyRequirementCalculator, EnergyCalculationResult, EnergyFormula, EnergyFormulaExtensions, EnergyRequirementInput, NutritionGoalType, NutritionGoalTypeExtensions, Fact (+1 more)

### Community 24 - "validators"
Cohesion: 0.10
Nodes (26): changeMyPassword(), getMyProfile(), respondToCheckIn(), upsertExerciseSetLogs(), createExercise(), deleteExercise(), getExercises(), updateExercise() (+18 more)

### Community 25 - "packages lock"
Cohesion: 0.06
Nodes (31): contentHash, requested, resolved, type, dependencies, net10.0, type, contentHash (+23 more)

### Community 26 - "packages lock"
Cohesion: 0.06
Nodes (31): contentHash, requested, resolved, type, dependencies, net10.0, type, contentHash (+23 more)

### Community 27 - "helpers"
Cohesion: 0.16
Nodes (23): getClient(), cancelSession(), completeSession(), createSession(), getSessions(), markSessionMissed(), ClientTable(), UpcomingSessions() (+15 more)

### Community 28 - "Client Store Tests"
Cohesion: 0.09
Nodes (16): ClientActivityState, SubscriptionState, ActivitySeed, ClientState, PersistedClientSeed, ClientState, CreateSeed, PersistedClientSeed (+8 more)

### Community 29 - "packages lock"
Cohesion: 0.07
Nodes (30): System.Composition.AttributedModel, System.Composition.Convention, System.Composition.Hosting, System.Composition.Runtime, System.Composition.TypedParts, System.Composition, System.Composition.Convention, System.Composition.Hosting (+22 more)

### Community 30 - "Catalog Reference Validation Tests"
Cohesion: 0.21
Nodes (11): CancellationToken, ClientSupplementAssignment, DateTime, Fact, Guid, MealPlan, Task, TestTenantSeed (+3 more)

### Community 31 - "packages lock"
Cohesion: 0.08
Nodes (29): Humanizer.Core, Microsoft.CodeAnalysis.Analyzers, Microsoft.CodeAnalysis.Common, Microsoft.CodeAnalysis.CSharp, System.Composition, contentHash, dependencies, resolved (+21 more)

### Community 32 - "Pagination Validation Rules Tests"
Cohesion: 0.11
Nodes (15): AbstractValidator, CreateClientCommand, CreateClientCommandValidator, ListClientsQuery, ListClientsQueryValidator, UpdateClientCommand, UpdateClientCommandValidator, PaginationValidationRulesTests (+7 more)

### Community 33 - "Trainer Subscription"
Cohesion: 0.14
Nodes (11): DateTime, Guid, TrainerSubscription, SubscriptionTier, Fact, TrainerSubscriptionTests, Fact, MemberData (+3 more)

### Community 34 - "Macro Target Calculator Tests"
Cohesion: 0.13
Nodes (11): MacroTargetCalculator, MacroCalculationResult, ManualMacroInput, PercentageMacroInput, PerKgMacroInput, Fact, MemberData, Theory (+3 more)

### Community 35 - "packages lock"
Cohesion: 0.07
Nodes (27): Docker.DotNet.Enhanced.Handler.Abstractions, contentHash, dependencies, resolved, type, contentHash, dependencies, resolved (+19 more)

### Community 36 - "nutrition Api"
Cohesion: 0.15
Nodes (21): getClients(), archiveMealPlan(), calculateMacros(), createFood(), createMealPlan(), deleteFood(), getActivityFactors(), getFoods() (+13 more)

### Community 37 - "packages lock"
Cohesion: 0.08
Nodes (26): Microsoft.Build.Framework, Microsoft.CodeAnalysis.CSharp.Workspaces, Microsoft.CodeAnalysis.Workspaces.Common, Microsoft.CodeAnalysis.Workspaces.MSBuild, Microsoft.Extensions.DependencyModel, Microsoft.VisualStudio.SolutionPersistence, Mono.TextTemplating, Newtonsoft.Json (+18 more)

### Community 38 - "Durable Job / Durable Job Configuration"
Cohesion: 0.12
Nodes (8): Domain.UnitTests.Entities.Assessments, Domain.UnitTests.ValueObjects, Domain.ValueObjects, Domain.Entities.Assessments, Domain.Entities.Jobs, Unit.Domain.UnitTests.ValueObjects, Infrastructure.Data.Configurations.Jobs, Domain.UnitTests.Entities.Jobs

### Community 39 - "packages lock"
Cohesion: 0.08
Nodes (26): dependencies, type, Application, Domain, FluentValidation, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Npgsql (+18 more)

### Community 40 - "assessments Api"
Cohesion: 0.16
Nodes (17): addCheckinNotes(), createAssessment(), createCheckin(), getAssessmentsByClient(), getCheckinsByClient(), skipCheckin(), updateAssessment(), getMyTrainingPlan() (+9 more)

### Community 41 - "tsconfig"
Cohesion: 0.08
Nodes (25): compilerOptions, allowSyntheticDefaultImports, baseUrl, esModuleInterop, forceConsistentCasingInFileNames, isolatedModules, jsx, lib (+17 more)

### Community 42 - "Postgres Constraint Translator Tests"
Cohesion: 0.15
Nodes (12): DomainException, PersistenceOperation, PostgresException, string, PostgresConstraintTranslator, Fact, InlineData, PostgresException (+4 more)

### Community 43 - "Meal Plan Configuration / Meal Plan Meal"
Cohesion: 0.10
Nodes (16): EntityTypeBuilder, TrainerSettingsConfiguration, EntityTypeBuilder, MealPlan, MealPlanConfiguration, EntityTypeBuilder, MealPlanMeal, MealPlanMealConfiguration (+8 more)

### Community 44 - "Postgres Container Fixture"
Cohesion: 0.11
Nodes (16): CancellationToken, DateTime, PostgreSqlContainer, string, Task, User, ValueTask, PostgresContainerFixture (+8 more)

### Community 45 - "packages lock"
Cohesion: 0.09
Nodes (22): dependencies, net10.0, type, contentHash, requested, resolved, type, contentHash (+14 more)

### Community 46 - "Notification Configuration"
Cohesion: 0.10
Nodes (12): EntityTypeBuilder, Notification, NotificationConfiguration, Domain.Entities.Sessions, Infrastructure.Data.Configurations.Sessions, Infrastructure.Data.Configurations.Notifications, Domain.UnitTests.Entities.Clients, Domain.Entities.TrainerSettings (+4 more)

### Community 47 - "packages lock"
Cohesion: 0.09
Nodes (23): Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options (+15 more)

### Community 48 - "packages lock"
Cohesion: 0.09
Nodes (23): Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options (+15 more)

### Community 49 - "tsconfig app"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleDetection, moduleResolution (+14 more)

### Community 50 - "Subscription Status Tests"
Cohesion: 0.11
Nodes (11): DateTime, Guid, ProcessedStripeEvent, EmailAddress, SubscriptionStatus, Fact, MemberData, Theory (+3 more)

### Community 51 - "Notification Tests"
Cohesion: 0.18
Nodes (7): DateTime, Guid, Notification, Fact, NotificationTests, Domain.UnitTests.Entities.Notifications, Domain.Entities.Notifications

### Community 52 - "packages lock"
Cohesion: 0.09
Nodes (22): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions (+14 more)

### Community 53 - "packages lock"
Cohesion: 0.09
Nodes (22): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions (+14 more)

### Community 54 - "packages lock"
Cohesion: 0.09
Nodes (22): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions (+14 more)

### Community 55 - "Client Store"
Cohesion: 0.32
Nodes (9): CreateClientStoreOutcome, ReactivateClientStoreOutcome, CancellationToken, Client, DateTime, Guid, Task, ClientStore (+1 more)

### Community 56 - "IClient Queries / Client Details Dto"
Cohesion: 0.13
Nodes (15): IClientQueries, CancellationToken, DateOnly, Guid, IReadOnlyList, Task, ClientDetailsDto, GetClientHandler (+7 more)

### Community 57 - "IClient Store"
Cohesion: 0.17
Nodes (14): IClientStore, CancellationToken, Client, DateTime, Guid, Task, ArchiveClientHandler, CancellationToken (+6 more)

### Community 58 - "Session"
Cohesion: 0.22
Nodes (6): DateTime, Guid, Session, SessionStatus, EntityTypeBuilder, SessionConfiguration

### Community 59 - "packages lock"
Cohesion: 0.10
Nodes (21): Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.Logging, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory (+13 more)

### Community 60 - "packages lock"
Cohesion: 0.10
Nodes (21): Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.Logging, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory (+13 more)

### Community 61 - "packages lock"
Cohesion: 0.10
Nodes (21): Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.Logging, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory (+13 more)

### Community 62 - "packages lock"
Cohesion: 0.10
Nodes (21): Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory, Microsoft.Extensions.Configuration.Abstractions, Microsoft.Extensions.Logging, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.Extensions.Caching.Memory (+13 more)

### Community 63 - "Processed Stripe Event Configuration / T"
Cohesion: 0.14
Nodes (9): EntityTypeBuilder, ProcessedStripeEvent, ProcessedStripeEventConfiguration, EntityTypeBuilder, TrainerSubscription, TrainerSubscriptionConfiguration, Infrastructure.Data.Configurations.Billing, Domain.UnitTests.Entities.Billing (+1 more)

### Community 64 - "Food"
Cohesion: 0.17
Nodes (7): DateTime, Guid, Food, EntityTypeBuilder, FoodConfiguration, Fact, FoodTests

### Community 65 - "Meal Plan"
Cohesion: 0.18
Nodes (8): DateOnly, DateTime, Guid, MealPlanMeal, MealPlan, MacroSummary, IReadOnlyCollection, List

### Community 66 - "Tenant Write Validation Interceptor"
Cohesion: 0.24
Nodes (11): CancellationToken, Guid, ITenantContext, PtManagerDbContext, Task, ValueTask, TenantWriteValidationInterceptor, DbContextEventData (+3 more)

### Community 67 - "Client Supplement Assignment Tests"
Cohesion: 0.23
Nodes (10): ClientSupplementAssignment, DateTime, Fact, Guid, Task, TestTenantSeed, ClientSupplementAssignmentTests, Supplement (+2 more)

### Community 68 - "package"
Cohesion: 0.11
Nodes (19): axios, dependencies, axios, lucide-react, @radix-ui/react-checkbox, react-day-picker, react-dom, recharts (+11 more)

### Community 69 - "packages lock"
Cohesion: 0.11
Nodes (19): Microsoft.ApplicationInsights, Microsoft.Testing.Platform, Microsoft.ApplicationInsights, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type (+11 more)

### Community 70 - "packages lock"
Cohesion: 0.11
Nodes (19): Microsoft.ApplicationInsights, Microsoft.Testing.Platform, Microsoft.ApplicationInsights, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type (+11 more)

### Community 71 - "packages lock"
Cohesion: 0.11
Nodes (19): BouncyCastle.Cryptography, Microsoft.Extensions.Logging.Abstractions, contentHash, dependencies, resolved, type, Microsoft.Extensions.Logging.Abstractions, Docker.DotNet.Enhanced.Handler.Abstractions (+11 more)

### Community 72 - "packages lock"
Cohesion: 0.11
Nodes (19): Microsoft.ApplicationInsights, Microsoft.Testing.Platform, Microsoft.ApplicationInsights, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type (+11 more)

### Community 73 - "Pack Type"
Cohesion: 0.20
Nodes (7): DateTime, Guid, PackType, EntityTypeBuilder, PackTypeConfiguration, Fact, PackTypeTests

### Community 74 - "User"
Cohesion: 0.26
Nodes (5): DateTime, Guid, User, Fact, UserTests

### Community 75 - "Meal Plan Meal"
Cohesion: 0.16
Nodes (11): DateTime, Guid, IReadOnlyCollection, List, MealPlanMeal, DateTime, Guid, MealPlanMealItem (+3 more)

### Community 76 - "Trainer Settings"
Cohesion: 0.18
Nodes (7): DateTime, Guid, TrainerSettings, DateTime, Fact, TrainerSettingsTests, Domain.UnitTests.Entities.TrainerSettings

### Community 77 - "packages lock"
Cohesion: 0.11
Nodes (17): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+9 more)

### Community 78 - "packages lock"
Cohesion: 0.11
Nodes (17): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+9 more)

### Community 79 - "tsconfig node"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, lib, module, moduleDetection, moduleResolution, noEmit (+9 more)

### Community 80 - "packages lock"
Cohesion: 0.12
Nodes (17): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Npgsql, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Npgsql, contentHash, dependencies (+9 more)

### Community 81 - "Invite Token Tests"
Cohesion: 0.18
Nodes (9): DateTime, Guid, InviteToken, EntityTypeBuilder, InviteTokenConfiguration, DateTime, Fact, Guid (+1 more)

### Community 82 - "Refresh Token Configuration"
Cohesion: 0.16
Nodes (9): EntityTypeBuilder, RefreshToken, RefreshTokenConfiguration, EntityTypeBuilder, User, UserConfiguration, Domain.Entities.Identity, Unit.Domain.UnitTests.Entities.Identity (+1 more)

### Community 83 - "packages lock"
Cohesion: 0.12
Nodes (17): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, contentHash, dependencies, resolved, type, contentHash, dependencies (+9 more)

### Community 84 - "packages lock"
Cohesion: 0.12
Nodes (17): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, contentHash, dependencies, resolved, type, contentHash, dependencies (+9 more)

### Community 85 - "launch Settings"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 86 - "Job Status Tests"
Cohesion: 0.23
Nodes (8): JobStatus, EntityTypeBuilder, OutboxMessageConfiguration, Fact, MemberData, Theory, TheoryData, JobStatusTests

### Community 87 - "20260804163659 Initial Create"
Cohesion: 0.13
Nodes (9): ModelBuilder, InitialCreate, InitialCreate, ModelBuilder, PtManagerDbContextModelSnapshot, Infrastructure.Data.Migrations, Migration, MigrationBuilder (+1 more)

### Community 88 - "packages lock"
Cohesion: 0.12
Nodes (16): dependencies, type, Application, Domain, FluentValidation, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, dependencies (+8 more)

### Community 89 - "packages lock"
Cohesion: 0.12
Nodes (16): dependencies, type, Application, Domain, FluentValidation, Microsoft.EntityFrameworkCore, Npgsql.EntityFrameworkCore.PostgreSQL, dependencies (+8 more)

### Community 90 - "Tenant Context Extensions Tests"
Cohesion: 0.16
Nodes (9): TenantContextExtensions, Guid, ITenantContext, StubTenantContext, TenantContextExtensionsTests, Fact, Guid, TenantOrigin (+1 more)

### Community 91 - "Exercise"
Cohesion: 0.22
Nodes (6): DateTime, Guid, Exercise, EntityTypeBuilder, ExerciseConfiguration, Fact

### Community 92 - "packages lock"
Cohesion: 0.13
Nodes (15): dependencies, type, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi (+7 more)

### Community 93 - "packages lock"
Cohesion: 0.13
Nodes (15): dependencies, type, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi (+7 more)

### Community 94 - "Layer Dependency Tests"
Cohesion: 0.20
Nodes (6): Assembly, LayerDependencyTests, Fact, InlineData, Theory, ArchitectureTests

### Community 95 - "packages lock"
Cohesion: 0.14
Nodes (14): dependencies, type, Application, Domain, FluentValidation, Npgsql.EntityFrameworkCore.PostgreSQL, dependencies, type (+6 more)

### Community 96 - "Training Plan"
Cohesion: 0.23
Nodes (6): DateOnly, DateTime, Guid, TrainingPlan, Fact, TrainingPlanTests

### Community 97 - "packages lock"
Cohesion: 0.14
Nodes (13): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+5 more)

### Community 98 - "Persistence Schema Metadata Tests"
Cohesion: 0.20
Nodes (5): PersistenceSchemaMetadataTests, Fact, IReadOnlyEntityType, IModel, PropertyMapping

### Community 99 - "packages lock"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 100 - "packages lock"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 101 - "Round Trip And Constraint Tests"
Cohesion: 0.31
Nodes (6): DateTime, Fact, InlineData, Task, Theory, RoundTripAndConstraintTests

### Community 102 - "packages lock"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 103 - "packages lock"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 104 - "Meal Plan Tests"
Cohesion: 0.29
Nodes (5): DateTime, Fact, InlineData, Theory, MealPlanTests

### Community 105 - "packages lock"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 106 - "chart"
Cohesion: 0.14
Nodes (11): react, useCarousel(), ChartConfig, ChartContainer, ChartContext, ChartContextProps, ChartLegendContent, ChartTooltipContent (+3 more)

### Community 107 - "Exercise Set Tests"
Cohesion: 0.19
Nodes (6): InlineData, Theory, ExerciseSetTests, ExerciseTests, Domain.Entities.Training, Domain.UnitTests.Entities.Training

### Community 108 - "packages lock"
Cohesion: 0.18
Nodes (13): Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, Microsoft.Extensions.Caching.Abstractions, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options, dependencies (+5 more)

### Community 109 - "packages lock"
Cohesion: 0.15
Nodes (13): Docker.DotNet.Enhanced, Docker.DotNet.Enhanced.X509, SharpZipLib, SSH.NET, Testcontainers, contentHash, dependencies, resolved (+5 more)

### Community 110 - "packages lock"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 111 - "packages lock"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 112 - "package"
Cohesion: 0.15
Nodes (13): eslint, devDependencies, eslint, jsdom, @testing-library/jest-dom, @types/node, typescript-eslint, @vitest/eslint-plugin (+5 more)

### Community 113 - "carousel"
Cohesion: 0.15
Nodes (12): Carousel, CarouselApi, CarouselContent, CarouselContext, CarouselContextProps, CarouselItem, CarouselNext, CarouselOptions (+4 more)

### Community 114 - "Client Session Pack"
Cohesion: 0.30
Nodes (6): DateOnly, DateTime, Guid, ClientSessionPack, EntityTypeBuilder, ClientSessionPackConfiguration

### Community 115 - "Refresh Token"
Cohesion: 0.27
Nodes (5): DateTime, Guid, RefreshToken, Fact, RefreshTokenTests

### Community 116 - "Food Macro Invariant Tests"
Cohesion: 0.27
Nodes (4): Domain.UnitTests.Services, Domain.UnitTests.Entities.Nutrition, Domain.Services, Domain.Entities.Nutrition

### Community 117 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Npgsql, Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Relational, Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL, contentHash (+4 more)

### Community 118 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 119 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Extensions.Primitives, Microsoft.Extensions.Primitives, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 120 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Extensions.Primitives, Microsoft.Extensions.Primitives, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 121 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Extensions.Primitives, Microsoft.Extensions.Primitives, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 122 - "packages lock"
Cohesion: 0.18
Nodes (12): Docker.DotNet.Enhanced.LegacyHttp, Docker.DotNet.Enhanced.NativeHttp, Docker.DotNet.Enhanced.NPipe, Docker.DotNet.Enhanced.Unix, contentHash, dependencies, resolved, type (+4 more)

### Community 123 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Testing.Platform, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 124 - "Training Plan Day Exercise Tests"
Cohesion: 0.23
Nodes (6): DateTime, Fact, Guid, InlineData, Theory, TrainingPlanDayExerciseTests

### Community 125 - "packages lock"
Cohesion: 0.17
Nodes (12): Microsoft.Testing.Platform, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 126 - "packages lock"
Cohesion: 0.18
Nodes (10): dependencies, net10.0, type, contentHash, requested, resolved, type, domain (+2 more)

### Community 127 - "Check In Configuration"
Cohesion: 0.22
Nodes (7): CheckIn, EntityTypeBuilder, CheckInConfiguration, EntityTypeBuilder, InitialAssessment, InitialAssessmentsConfiguration, Infrastructure.Data.Configurations.Assessments

### Community 128 - "Nutrition Model Metadata Tests"
Cohesion: 0.27
Nodes (3): NutritionModelMetadataTests, Fact, IReadOnlyEntityType

### Community 129 - "packages lock"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 130 - "packages lock"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 131 - "packages lock"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 132 - "packages lock"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 133 - "Meal Plan Calculation Tests"
Cohesion: 0.40
Nodes (3): DateTime, Fact, MealPlanCalculationTests

### Community 134 - "Session Tests"
Cohesion: 0.42
Nodes (3): DateTime, Fact, SessionTests

### Community 135 - "packages lock"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 136 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, Microsoft.EntityFrameworkCore.Abstractions, Microsoft.EntityFrameworkCore.Analyzers, contentHash, dependencies, requested, resolved (+2 more)

### Community 137 - "Client Mappings"
Cohesion: 0.20
Nodes (6): ClientErrors, ClientMappings, Client, ClientSessionPack, IReadOnlyList, Application.Features.Clients

### Community 138 - "Client Exercise Set Log"
Cohesion: 0.29
Nodes (5): DateTime, Guid, ClientExerciseSetLog, EntityTypeBuilder, ClientExerciseSetLogConfiguration

### Community 139 - "Nutrition Calculation Snapshot"
Cohesion: 0.40
Nodes (4): DateTime, int, NutritionCalculationSnapshot, string

### Community 140 - "System Clock"
Cohesion: 0.22
Nodes (7): DateTime, SystemClock, DateTime, TestClock, StubClock, Infrastructure.Time, IClock

### Community 141 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 142 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.EntityFrameworkCore.Relational, Npgsql, Microsoft.EntityFrameworkCore.Relational, Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL, contentHash, dependencies, requested (+2 more)

### Community 143 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 144 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.EntityFrameworkCore.Relational, Npgsql, Microsoft.EntityFrameworkCore.Relational, Npgsql, Npgsql.EntityFrameworkCore.PostgreSQL, contentHash, dependencies, requested (+2 more)

### Community 145 - "Tenant Write Validation Interceptor Test"
Cohesion: 0.33
Nodes (6): Client, DateTime, Fact, Guid, Task, TenantWriteValidationInterceptorTests

### Community 146 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 147 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 148 - "packages lock"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 149 - "package"
Cohesion: 0.20
Nodes (10): scripts, build, dev, format, format:check, lint, preview, test (+2 more)

### Community 150 - "context-menu"
Cohesion: 0.20
Nodes (9): ContextMenuCheckboxItem, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuRadioItem, ContextMenuSeparator, ContextMenuShortcut(), ContextMenuSubContent (+1 more)

### Community 151 - "Exercise Set"
Cohesion: 0.25
Nodes (5): DateTime, Guid, ExerciseSet, EntityTypeBuilder, ExerciseSetConfiguration

### Community 152 - "packages lock"
Cohesion: 0.22
Nodes (9): Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options, contentHash, dependencies, resolved, type (+1 more)

### Community 153 - "Tenant Query Filter Tests"
Cohesion: 0.33
Nodes (4): Fact, Task, TenantQueryFilterTests, Infrastructure.IntegrationTests.Tenancy

### Community 154 - "packages lock"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 155 - "Food Macro Invariant Tests"
Cohesion: 0.25
Nodes (6): DateTime, Fact, MemberData, Theory, TheoryData, FoodMacroInvariantTests

### Community 156 - "packages lock"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 157 - "handlers"
Cohesion: 0.28
Nodes (6): CLIENT_USER, handlers, TEST_IDS, TRAINER_SETTINGS, TRAINER_USER, server

### Community 158 - "packages lock"
Cohesion: 0.25
Nodes (8): Microsoft.OpenApi, Microsoft.OpenApi, contentHash, dependencies, requested, resolved, type, Microsoft.AspNetCore.OpenApi

### Community 159 - "Validation Result Extension Tests"
Cohesion: 0.32
Nodes (4): ValidationResultExtension, ValidationResultExtensionTests, Fact, ValidationResult

### Community 160 - "Training Plan Day Configuration"
Cohesion: 0.29
Nodes (5): DateTime, Guid, TrainingPlanDay, EntityTypeBuilder, TrainingPlanDayConfiguration

### Community 161 - "Training Plan Day Exercise Configuration"
Cohesion: 0.29
Nodes (5): DateTime, Guid, TrainingPlanDayExercise, EntityTypeBuilder, TrainingPlanDayExerciseConfiguration

### Community 162 - "packages lock"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 163 - "packages lock"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 164 - "packages lock"
Cohesion: 0.25
Nodes (8): Testcontainers, Testcontainers.PostgreSql, contentHash, dependencies, requested, resolved, type, Testcontainers

### Community 165 - "packages lock"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 166 - "packages lock"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 167 - "Client Session Pack Tests"
Cohesion: 0.43
Nodes (4): DateTime, Fact, Guid, ClientSessionPackTests

### Community 168 - "packages lock"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 169 - "Email Address Tests"
Cohesion: 0.32
Nodes (4): Fact, InlineData, Theory, EmailAddressTests

### Community 170 - "packages lock"
Cohesion: 0.29
Nodes (7): System.CodeDom, contentHash, dependencies, resolved, type, Mono.TextTemplating, System.CodeDom

### Community 171 - "packages lock"
Cohesion: 0.29
Nodes (7): dependencies, type, Domain, FluentValidation, Domain, FluentValidation, application

### Community 172 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 173 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 174 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 175 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 176 - "Catalog Reference Validation Tests"
Cohesion: 0.29
Nodes (6): TrainingPlan, TrainingPlanDay, TrainingPlanDayExercise, Day, Plan, Prescription

### Community 177 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 178 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 179 - "packages lock"
Cohesion: 0.29
Nodes (7): dependencies, type, Domain, FluentValidation, Domain, FluentValidation, application

### Community 180 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.ApplicationInsights, Microsoft.ApplicationInsights, contentHash, dependencies, resolved, type, Microsoft.Testing.Extensions.Telemetry

### Community 181 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 182 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 183 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.ApplicationInsights, Microsoft.ApplicationInsights, contentHash, dependencies, resolved, type, Microsoft.Testing.Extensions.Telemetry

### Community 184 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 185 - "packages lock"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 186 - "compose"
Cohesion: 0.40
Nodes (6): postgres service (ptmanager-postgres-dev), ptmanager-local Docker Compose Stack, ptmanager_postgres_data volume, Frontend index.html (Vite entry), #root mount element, src/main.jsx

### Community 187 - "Pagination Validation Rules"
Cohesion: 0.33
Nodes (4): PaginationValidationRules, AbstractValidator, Expression, Func

### Community 188 - "Training Plan Configuration"
Cohesion: 0.33
Nodes (3): EntityTypeBuilder, TrainingPlanConfiguration, Infrastructure.Data.Configurations.Training

### Community 189 - "Test Tenant Context"
Cohesion: 0.40
Nodes (3): Guid, TenantOrigin, TestTenantContext

### Community 190 - "Client Exercise Set Log Tests"
Cohesion: 0.47
Nodes (3): InlineData, Theory, ClientExerciseSetLogTests

### Community 191 - "Training Adjustments Tests"
Cohesion: 0.40
Nodes (3): DateTime, Fact, TrainingAdjustmentsTests

### Community 193 - "Authcontext"
Cohesion: 0.47
Nodes (4): applyBrandColor(), AuthContext, AuthProvider(), hexToHSL()

### Community 194 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 195 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 196 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, dependencies, resolved, type, Microsoft.Extensions.Logging.Abstractions

### Community 197 - "packages lock"
Cohesion: 0.40
Nodes (5): Npgsql, contentHash, dependencies, resolved, type

### Community 198 - "Jobs Model Metadata Tests"
Cohesion: 0.40
Nodes (3): Fact, JobsModelMetadataTests, IDisposable

### Community 199 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 200 - "packages lock"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 201 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 202 - "packages lock"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 203 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 204 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 205 - "packages lock"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 206 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 207 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, FluentValidation

### Community 208 - "packages lock"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 209 - "Nutrition Adjustments Tests"
Cohesion: 0.50
Nodes (3): DateTime, Fact, NutritionAdjustmentsTests

### Community 210 - "packages lock"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 211 - "packages lock"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 213 - "package"
Cohesion: 0.40
Nodes (4): name, private, type, version

### Community 214 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Humanizer.Core

### Community 215 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Build.Framework

### Community 216 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeAnalysis.Analyzers

### Community 217 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.EntityFrameworkCore.Abstractions

### Community 218 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.EntityFrameworkCore.Analyzers

### Community 219 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.DependencyModel

### Community 220 - "packages lock"
Cohesion: 0.50
Nodes (4): Npgsql, contentHash, resolved, type

### Community 221 - "packages lock"
Cohesion: 0.50
Nodes (4): System.CodeDom, contentHash, resolved, type

### Community 222 - "packages lock"
Cohesion: 0.50
Nodes (4): System.Composition.Runtime, contentHash, resolved, type

### Community 223 - "packages lock"
Cohesion: 0.50
Nodes (3): dependencies, net10.0, version

### Community 225 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.Caching.Memory

### Community 226 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.DependencyInjection.Abstractions

### Community 227 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.Primitives

### Community 228 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Bcl.AsyncInterfaces

### Community 229 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 230 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.EntityFrameworkCore.Analyzers

### Community 231 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.DependencyInjection.Abstractions

### Community 232 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.Primitives

### Community 233 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 234 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 235 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 236 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Bcl.AsyncInterfaces

### Community 237 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 238 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.EntityFrameworkCore.Analyzers

### Community 239 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.DependencyInjection.Abstractions

### Community 240 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.Primitives

### Community 241 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 242 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 243 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 244 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, BouncyCastle.Cryptography

### Community 245 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Extensions.DependencyInjection.Abstractions

### Community 246 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 247 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 248 - "packages lock"
Cohesion: 0.50
Nodes (4): SharpZipLib, contentHash, resolved, type

### Community 249 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

### Community 250 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Bcl.AsyncInterfaces

### Community 251 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 252 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 253 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 254 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 255 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 256 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 257 - "packages lock"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 258 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 259 - "packages lock"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

### Community 261 - "ci"
Cohesion: 1.00
Nodes (3): CI/CD Pipeline (Placeholder, deferred to future sprints), CD Placeholder Workflow, CI Placeholder Workflow

## Ambiguous Edges - Review These
- `ptmanager-local Docker Compose Stack` → `Frontend index.html (Vite entry)`  [AMBIGUOUS]
  backend/compose.yml · relation: conceptually_related_to

## Knowledge Gaps
- **1212 isolated node(s):** `$schema`, `commandName`, `dotnetRunMessages`, `launchBrowser`, `applicationUrl` (+1207 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **58 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What is the exact relationship between `ptmanager-local Docker Compose Stack` and `Frontend index.html (Vite entry)`?**
  _Edge tagged AMBIGUOUS (relation: conceptually_related_to) - confidence is low._
- **Why does `Application.Common.Abstractions` connect `Dependency Injection / IClock` to `Pagination Validation Rules Tests`, `System Clock`, `Notification Configuration`, `Archive Client Command / Archive Client`, `Tenant Context Extensions Tests`, `Test Tenant Context`?**
  _High betweenness centrality (0.039) - this node is a cross-community bridge._
- **Why does `Domain.ValueObjects` connect `Durable Job / Durable Job Configuration` to `Macro Distribution Mode`, `Meal Plan`, `Macro Target Calculator Tests`, `Initial Assessment Tests`, `Trainer Subscription`, `Client Tests`, `Notification Configuration`, `Invite Token Tests`, `Subscription Status Tests`, `Notification Tests`, `Food Macro Invariant Tests`, `Refresh Token Configuration`, `Energy Requirement Calculator`, `Session`, `Processed Stripe Event Configuration / T`?**
  _High betweenness centrality (0.038) - this node is a cross-community bridge._
- **Why does `PtManagerDbContext` connect `Pt Manager Db Context` to `Client Store Tests`, `Durable Job Repository Tests`, `Nutrition Model Metadata Tests`, `Persistence Schema Metadata Tests`, `Pt Manager Db Context`, `Jobs Model Metadata Tests`, `Dependency Injection / IClock`, `Initial Create Migration Tests`, `Client Store`, `Catalog Reference Validation Tests`?**
  _High betweenness centrality (0.035) - this node is a cross-community bridge._
- **What connects `$schema`, `commandName`, `dotnetRunMessages` to the rest of the system?**
  _1215 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Client Store Tests` be split into smaller, more focused modules?**
  _Cohesion score 0.05487061373797589 - nodes in this community are weakly interconnected._
- **Should `Durable Job Repository Tests` be split into smaller, more focused modules?**
  _Cohesion score 0.07151515151515152 - nodes in this community are weakly interconnected._