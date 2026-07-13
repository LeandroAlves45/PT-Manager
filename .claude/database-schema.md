# PT Manager — Database Schema

PostgreSQL 16 (SQLite em dev local). ORM: SQLModel. Migrations: SQL numerado em `backend/app/db/migrations/`.

## Entidades Principais

### Utilizadores e Auth

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `User` | `user.py` | Trainers, clients, superuser. `role`, `owner_trainer_id` |
| `ActiveToken` | `active_token.py` | Tokens de sessão activos (hash) |
| `RefreshToken` | `refresh_tokens.py` | Refresh tokens com grace period |

### Clientes e Avaliações

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `Client` | `client.py` | Perfil do cliente, ligado a `trainer_id` |
| `InitialAssessment` | `initial_assessment.py` | Avaliação inicial do cliente |
| `CheckIn` | `checkin.py` | Check-ins periódicos |

### Sessões e Packs

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `TrainingSession` | `session.py` | Sessões de treino agendadas/realizadas |
| `PackConsumption` | `session.py` | Consumo de créditos de pack |
| `PackType` | `pack.py` | Tipos de pack (catálogo) |
| `ClientPack` | `pack.py` | Pack comprado por cliente |

### Treino

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `Exercise` | `training.py` | Exercícios (catálogo global + por trainer) |
| `TrainingPlan` | `training.py` | Plano de treino |
| `TrainingPlanDay` | `training.py` | Dias do plano |
| `PlanDayExercise` | `training.py` | Exercícios por dia |
| `PlanExerciseSetLoad` | `training.py` | Séries/reps/carga |
| `ClientActivePlan` | `training.py` | Plano activo do cliente |
| `ClientExerciseSetLog` | `training.py` | Log de execução |

### Nutrição

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `Food` | `nutrition.py` | Alimentos (catálogo) |
| `MealPlan` | `nutrition.py` | Plano de refeições |
| `MealPlanMeal` | `nutrition.py` | Refeições do plano |
| `MealPlanItem` | `nutrition.py` | Itens/alimentos por refeição |
| `MealPlanMealSupplement` | `nutrition.py` | Suplementos por refeição |

### Suplementos

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `Supplement` | `supplement.py` | Catálogo de suplementos |
| `ClientSupplement` | `client_supplement.py` | Suplementos atribuídos a cliente |

### Billing e Config

| Model | Ficheiro | Descrição |
|-------|----------|-----------|
| `TrainerSubscription` | `trainer_subscription.py` | Subscrição Stripe (FREE/STARTER/PRO) |
| `TrainerSettings` | `trainer_settings.py` | Configurações do trainer |
| `ProcessedStripeEvent` | `processed_stripe_event.py` | Idempotência de webhooks |
| `Notification` | `notification.py` | Notificações in-app (JSONB payload) |

## Multi-Tenant

A maioria das tabelas tem `trainer_id` (directo ou via relação com `Client`/`User`).

**Regra:** queries de dados de cliente/sessão/plano devem sempre filtrar pelo `trainer_id` do utilizador autenticado.

## Migrations

- Localização: `backend/app/db/migrations/`
- Formato: `NNN_descricao.sql` (ex: `027_email_verification_token_columns.sql`)
- Runner: `python -m app.db.migrate_runner`
- Registo: tabela `schema_migrations`
- **Nunca editar** ficheiros SQL já aplicados — criar novo numerado

## Índices e Performance

Ver migrations existentes para índices criados. Para novas queries frequentes, adicionar índice na mesma migration que introduz a coluna.

Referência PostgreSQL: skill `supabase-postgres-best-practices` (secções de indexing/query, ignorar RLS Supabase).
