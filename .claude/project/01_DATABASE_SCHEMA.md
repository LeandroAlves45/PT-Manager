# PT Manager — Database Schema v3.0 (PostgreSQL 16 / EF Core 10)

*Schema Definition — Julho 2026*

Estado: alinhado com `00_ARCHITECTURE.md` v3.0. A implementação ainda não existe.

---

## Overview

Este documento é a **especificação do modelo EF Core alvo**, não um script SQL a correr manualmente. A migration `InitialCreate` é gerada a partir deste modelo com `dotnet ef migrations add InitialCreate` (ver `01_DATABASE_SCHEMA.md §12` e `03_DEVELOPER_GUIDE.md`). O histórico de migrations SQL do Python não é convertido — não existem dados de produção a preservar (ver `00_ARCHITECTURE.md §1` e `§7.3`).

**Características:**
- Multi-tenancy: `owner_trainer_id` em todas as tabelas de negócio, aplicado via EF Core Global Query Filters ligados a `ITenantContext` (nunca a `HttpContext` diretamente — jobs e webhooks não têm contexto HTTP)
- IDs: `uuid` nativo (ver decisão §1 abaixo)
- Soft delete: `is_deleted` flag
- Timestamps: `created_at`, `updated_at` em UTC
- Constraints: FK cascades, unique indexes
- Generated columns: `kcal` calculado automaticamente
- Novas tabelas de infraestrutura: `durable_jobs`, `outbox_messages`, `refresh_tokens` (substituem `active_tokens` + RabbitMQ, ver `00_ARCHITECTURE.md §9` e `§10.3`)

---

## Decisão 1 — Tipo de ID: `uuid` nativo

**Decisão:** todas as PKs/FKs passam de `VARCHAR(36)` (string GUID herdada do Python) para `uuid` nativo do PostgreSQL, mapeado para `Guid` em C#.

**Trade-off avaliado:**

| | `uuid` nativo (escolhido) | `VARCHAR(36)` (schema Python) |
|---|---|---|
| Tamanho índice | 16 bytes | 36+ bytes |
| Comparação/JOIN | Comparação binária nativa, mais rápida | Comparação de string |
| Mapeamento C# | `Guid` direto, zero parsing | `string` com parsing/validação manual |
| Custo de migração | Nenhum — schema greenfield, sem dados de produção a preservar | N/A |
| Compatibilidade com Python em paralelo | Precisa de cast explícito se algum dia coexistirem | Direta |

Não há necessidade de comparar dados entre os dois backends em produção (o Python nem vai ao Git), por isso o único trade-off real (facilidade de comparação cross-stack) não se aplica. **Geração:** os IDs são gerados no lado da aplicação (`Guid.NewGuid()` via EF Core value generator), não com `gen_random_uuid()` do Postgres — evita depender da extensão `pgcrypto` e mantém a geração testável/determinística em testes unitários.

```csharp
// Domain — value generator, sem dependência de extensão Postgres
public Guid Id { get; private set; } = Guid.NewGuid();
```

```sql
-- Coluna resultante em qualquer tabela de negócio
id UUID PRIMARY KEY
```

---

## Decisão 2 — Identity: tabela `users` própria, não o schema padrão do ASP.NET Core Identity

**Decisão:** manter uma tabela `users` própria e simplificada (compatível com o que o frontend já espera), implementando `IUserStore<User>`/`IUserPasswordStore<User>` customizados sobre ela — em vez de adotar o schema completo por omissão do Identity (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, etc.).

**Trade-off avaliado:**

| | `users` custom + stores customizados (escolhido) | Schema padrão do Identity |
|---|---|---|
| Compatibilidade com frontend | Direta — mesma forma que o Python expunha (`role`, `owner_trainer_id`) | Precisa de camada de tradução/DTO extra |
| Nº de tabelas | 1 tabela de utilizadores | 5+ tabelas (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`) |
| Roles | Coluna simples `role` (`trainer`/`client`/`superuser`) — já suficiente, sem necessidade de múltiplas roles por utilizador | Modelo N:N completo, over-engineering para 3 roles fixas |
| Esforço de implementação | Custom store exige implementar as interfaces do Identity manualmente | Zero esforço, mas schema desalinhado com o domínio |
| Funcionalidade aproveitada do Identity | Hashing de password, lockout, password policies (via `PasswordHasher<User>`, `UserManager<User>` configurado com o store custom) | Tudo, incluindo o que não é preciso |

A tabela `users` ganha as colunas mínimas exigidas pelas interfaces do Identity que vamos efetivamente usar (hashing, lockout, verificação de email):

```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    normalized_email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    security_stamp VARCHAR(255) NOT NULL,
    concurrency_stamp VARCHAR(255) NOT NULL,
    full_name VARCHAR(255),
    role VARCHAR(50) NOT NULL DEFAULT 'trainer', -- 'trainer', 'client', 'superuser'
    email_confirmed BOOLEAN DEFAULT false,
    lockout_end TIMESTAMPTZ,
    access_failed_count INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT normalized_email_unique UNIQUE (normalized_email),
    CONSTRAINT role_check CHECK (role IN ('trainer', 'client', 'superuser'))
);

CREATE INDEX idx_users_normalized_email ON users(normalized_email);
CREATE INDEX idx_users_role ON users(role);
CREATE INDEX idx_users_is_deleted ON users(is_deleted);
```

Nota: todos os `TIMESTAMP` do schema anterior passam a `TIMESTAMPTZ` (com timezone) — evita ambiguidade de UTC vs. local em produção.

---

## Tabelas Core

### 2. `trainer_subscriptions`

Subscription status por trainer.

```sql
CREATE TABLE trainer_subscriptions (
    id UUID PRIMARY KEY,
    trainer_id UUID NOT NULL,
    subscription_status VARCHAR(50) NOT NULL DEFAULT 'FREE',
    subscription_tier VARCHAR(50) NOT NULL DEFAULT 'FREE', -- FREE, STARTER, PRO
    client_limit INTEGER NOT NULL DEFAULT 5,
    current_client_count INTEGER DEFAULT 0,
    is_exempt_from_billing BOOLEAN DEFAULT false,
    trial_ends_at TIMESTAMPTZ,
    stripe_subscription_id VARCHAR(255),
    stripe_customer_id VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT status_check CHECK (subscription_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'CANCELLED')),
    CONSTRAINT tier_check CHECK (subscription_tier IN ('FREE', 'STARTER', 'PRO'))
);

CREATE INDEX idx_subscriptions_trainer ON trainer_subscriptions(trainer_id);
CREATE INDEX idx_subscriptions_status ON trainer_subscriptions(subscription_status);
```

### 3. `clients`

Clientes do trainer (multi-tenant).

```sql
CREATE TABLE clients (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    user_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    objective VARCHAR(255), -- 'weight_loss', 'muscle_gain', 'strength', 'endurance'
    bio TEXT,
    avatar_url VARCHAR(500),
    is_active BOOLEAN DEFAULT true,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT unique_client_per_trainer UNIQUE(owner_trainer_id, user_id)
);

CREATE INDEX idx_clients_trainer ON clients(owner_trainer_id);
CREATE INDEX idx_clients_is_deleted ON clients(is_deleted);
```

### 4. `trainer_settings`

Branding + configurações por trainer.

```sql
CREATE TABLE trainer_settings (
    id UUID PRIMARY KEY,
    trainer_id UUID NOT NULL UNIQUE,
    app_name VARCHAR(255) DEFAULT 'PT Manager',
    logo_url VARCHAR(500),
    logo_public_id VARCHAR(500), -- Cloudinary public_id para deletar depois
    primary_color VARCHAR(7) DEFAULT '#000000',
    body_color VARCHAR(7) DEFAULT '#FFFFFF',
    background_image_url VARCHAR(500),
    phone VARCHAR(20),
    address VARCHAR(500),
    city VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_settings_trainer ON trainer_settings(trainer_id);
```

### 5. `refresh_tokens`

Substitui o `active_tokens` do Python. Cobre rotação com deteção de reuso e revogação de família (`00_ARCHITECTURE.md §5.2`). Apenas o hash do token é persistido — nunca o valor em claro.

```sql
CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    family_id UUID NOT NULL, -- agrupa toda a cadeia de rotação de uma sessão
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    rotated_from_id UUID, -- token anterior na cadeia, null no primeiro
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_rotated_from FOREIGN KEY (rotated_from_id) REFERENCES refresh_tokens(id) ON DELETE SET NULL
);

CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_family ON refresh_tokens(family_id);
CREATE INDEX idx_refresh_tokens_expires ON refresh_tokens(expires_at);
```

**Deteção de reuso:** ao receber um refresh token, se `revoked_at IS NOT NULL` (já rodado antes), revoga-se toda a `family_id` — indica um token roubado a ser reutilizado.

### 6. `invite_tokens`

Convites para clientes.

```sql
CREATE TABLE invite_tokens (
    id UUID PRIMARY KEY,
    trainer_id UUID NOT NULL,
    email VARCHAR(255) NOT NULL,
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    is_used BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_invites_trainer ON invite_tokens(trainer_id);
CREATE INDEX idx_invites_email ON invite_tokens(email);
```

---

## Tabelas Nutrição

### 7. `foods`

Catálogo global de alimentos (owner_trainer_id = NULL).

```sql
CREATE TABLE foods (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID, -- NULL = global catalogue
    name VARCHAR(255) NOT NULL,
    description TEXT,
    calories DECIMAL(10, 2) NOT NULL,
    protein DECIMAL(10, 2) NOT NULL,
    carbs DECIMAL(10, 2) NOT NULL,
    fats DECIMAL(10, 2) NOT NULL,
    kcal DECIMAL(10, 2) GENERATED ALWAYS AS (
        protein * 4 + carbs * 4 + fats * 9
    ) STORED,
    fiber DECIMAL(10, 2),
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_foods_name ON foods(name);
CREATE INDEX idx_foods_trainer ON foods(owner_trainer_id);
CREATE INDEX idx_foods_is_deleted ON foods(is_deleted);
```

### 8. `supplements`

Suplementos globais (created_by_user_id = NULL para global).

```sql
CREATE TABLE supplements (
    id UUID PRIMARY KEY,
    created_by_user_id UUID, -- NULL = global
    name VARCHAR(255) NOT NULL,
    description TEXT,
    unit_of_measure VARCHAR(50), -- 'grams', 'capsules', 'ml', 'tablets'
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_creator FOREIGN KEY (created_by_user_id) REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX idx_supplements_name ON supplements(name);
CREATE INDEX idx_supplements_is_deleted ON supplements(is_deleted);
```

### 9. `meal_plans`

Planos alimentares por cliente.

```sql
CREATE TABLE meal_plans (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    starts_date DATE NOT NULL,
    ends_date DATE, -- opcional: sem valor, o plano fica ativo até ser substituído
    protein_target_g DECIMAL(10, 2) NOT NULL,
    carbs_target_g DECIMAL(10, 2) NOT NULL,
    fats_target DECIMAL(10, 2) NOT NULL,
    is_active BOOLEAN DEFAULT true,
    is_archived BOOLEAN DEFAULT false,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE,
    -- ends_date pode ser NULL (plano sem data de fim); a comparação com NULL
    -- avalia a UNKNOWN em Postgres, e uma CHECK só rejeita quando avalia a
    -- FALSE — logo esta constraint continua correta sem tratamento especial.
    CONSTRAINT date_order CHECK (starts_date <= ends_date)
);

CREATE INDEX idx_meal_plans_trainer ON meal_plans(owner_trainer_id);
CREATE INDEX idx_meal_plans_client ON meal_plans(client_id);
CREATE INDEX idx_meal_plans_is_active ON meal_plans(is_active);
```

### 10. `meal_plan_meals`

Refeições dentro dum plano.

```sql
CREATE TABLE meal_plan_meals (
    id UUID PRIMARY KEY,
    meal_plan_id UUID NOT NULL,
    meal_type VARCHAR(50) NOT NULL, -- texto livre definido pelo trainer (ex: 'breakfast', 'pre-treino', 'ceia')
    order_number INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_meal_plan FOREIGN KEY (meal_plan_id) REFERENCES meal_plans(id) ON DELETE CASCADE,
    CONSTRAINT meal_type_not_blank CHECK (length(trim(meal_type)) > 0),
    CONSTRAINT unique_meal_order UNIQUE(meal_plan_id, order_number)
);

CREATE INDEX idx_meals_plan ON meal_plan_meals(meal_plan_id);
```

### 11. `meal_plan_meal_items`

Alimentos numa refeição.

```sql
CREATE TABLE meal_plan_meal_items (
    id UUID PRIMARY KEY,
    meal_plan_meal_id UUID NOT NULL,
    food_id UUID NOT NULL,
    quantity_grams DECIMAL(10, 2) NOT NULL,
    order_number INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_meal FOREIGN KEY (meal_plan_meal_id) REFERENCES meal_plan_meals(id) ON DELETE CASCADE,
    CONSTRAINT fk_food FOREIGN KEY (food_id) REFERENCES foods(id) ON DELETE CASCADE,
    CONSTRAINT positive_quantity CHECK (quantity_grams > 0)
);

CREATE INDEX idx_items_meal ON meal_plan_meal_items(meal_plan_meal_id);
```

### 12. `meal_plan_meal_supplements`

Suplementos por refeição (com notas).

```sql
CREATE TABLE meal_plan_meal_supplements (
    id UUID PRIMARY KEY,
    meal_plan_meal_id UUID NOT NULL,
    supplement_id UUID NOT NULL,
    notes VARCHAR(500), -- 'dose: 2g', 'timing: with breakfast', etc
    quantity DECIMAL(10, 2) NOT NULL, -- unidade vem de supplements.unit_of_measure, não se duplica aqui
    order_number INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_meal FOREIGN KEY (meal_plan_meal_id) REFERENCES meal_plan_meals(id) ON DELETE CASCADE,
    CONSTRAINT fk_supplement FOREIGN KEY (supplement_id) REFERENCES supplements(id) ON DELETE CASCADE,
    CONSTRAINT unique_supplement_per_meal UNIQUE(meal_plan_meal_id, supplement_id),
    CONSTRAINT positive_supplement_quantity CHECK (quantity > 0)
);

CREATE INDEX idx_supp_meal ON meal_plan_meal_supplements(meal_plan_meal_id);
```

---

## Tabelas Treino

### 13. `training_plans`

Planos de treino.

```sql
CREATE TABLE training_plans (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    training_modality VARCHAR(50), -- 'strength', 'cardio', 'flexibility', 'mixed'
    notes TEXT,
    starts_date DATE NOT NULL,
    ends_date DATE, -- opcional: sem valor, o plano fica ativo até ser substituído/arquivado
    is_active BOOLEAN DEFAULT true,
    is_archived BOOLEAN DEFAULT false,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE
);

CREATE INDEX idx_training_plans_trainer ON training_plans(owner_trainer_id);
CREATE INDEX idx_training_plans_client ON training_plans(client_id);
```

### 14. `training_plan_days`

Dias dum plano (segunda, terça, etc).

```sql
CREATE TABLE training_plan_days (
    id UUID PRIMARY KEY,
    training_plan_id UUID NOT NULL,
    day_of_week INTEGER NOT NULL, -- 0 = Monday, 6 = Sunday
    week_number INTEGER NOT NULL,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_plan FOREIGN KEY (training_plan_id) REFERENCES training_plans(id) ON DELETE CASCADE,
    CONSTRAINT day_range CHECK (day_of_week >= 0 AND day_of_week <= 6),
    CONSTRAINT week_range CHECK (week_number >= 1 AND week_number <= 52)
);

CREATE INDEX idx_days_plan ON training_plan_days(training_plan_id);
```

### 15. `exercises`

Catálogo global de exercícios (owner_trainer_id = NULL).

```sql
CREATE TABLE exercises (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID, -- NULL = global
    name VARCHAR(255) NOT NULL,
    description TEXT,
    muscle_groups VARCHAR(500), -- 'chest,triceps'
    equipment VARCHAR(255),
    difficulty_level VARCHAR(50), -- 'beginner', 'intermediate', 'advanced'
    video_url VARCHAR(500),
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_exercises_name ON exercises(name);
CREATE INDEX idx_exercises_trainer ON exercises(owner_trainer_id);
```

### 16. `training_plan_day_exercises`

Exercícios dum dia de treino.

```sql
CREATE TABLE training_plan_day_exercises (
    id UUID PRIMARY KEY,
    training_plan_day_id UUID NOT NULL,
    exercise_id UUID NOT NULL,
    order_number INTEGER NOT NULL,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_day FOREIGN KEY (training_plan_day_id) REFERENCES training_plan_days(id) ON DELETE CASCADE,
    CONSTRAINT fk_exercise FOREIGN KEY (exercise_id) REFERENCES exercises(id) ON DELETE CASCADE
);

CREATE INDEX idx_day_exercises_day ON training_plan_day_exercises(training_plan_day_id);
```

### 17. `exercise_sets`

Series/sets de um exercício.

```sql
CREATE TABLE exercise_sets (
    id UUID PRIMARY KEY,
    training_plan_day_exercise_id UUID NOT NULL,
    set_number INTEGER NOT NULL,
    planned_reps INTEGER,
    planned_weight_kg DECIMAL(10, 2),
    rest_seconds_min INTEGER,
    rest_seconds_max INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_day_exercise FOREIGN KEY (training_plan_day_exercise_id) REFERENCES training_plan_day_exercises(id) ON DELETE CASCADE,
    CONSTRAINT set_num_check CHECK (set_number >= 1 AND set_number <= 15),
    CONSTRAINT reps_check CHECK (planned_reps IS NULL OR planned_reps > 0),
    CONSTRAINT rest_range_check CHECK (
        rest_seconds_min IS NULL OR rest_seconds_max IS NULL OR rest_seconds_min <= rest_seconds_max
    )
);

CREATE INDEX idx_sets_exercise ON exercise_sets(training_plan_day_exercise_id);
```

### 18. `client_exercise_set_logs`

Registo real de series pelo cliente.

```sql
CREATE TABLE client_exercise_set_logs (
    id UUID PRIMARY KEY,
    client_id UUID NOT NULL,
    training_plan_day_exercise_id UUID NOT NULL,
    set_number INTEGER NOT NULL,
    weight_kg DECIMAL(10, 2) NOT NULL,
    reps_done INTEGER NOT NULL,
    notes VARCHAR(500),
    logged_at TIMESTAMPTZ NOT NULL, -- Official timestamp (not created_at)
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE,
    CONSTRAINT fk_day_exercise FOREIGN KEY (training_plan_day_exercise_id)
        REFERENCES training_plan_day_exercises(id) ON DELETE CASCADE,
    CONSTRAINT set_num_check CHECK (set_number >= 1 AND set_number <= 15),
    CONSTRAINT weight_check CHECK (weight_kg >= 0),
    CONSTRAINT reps_check CHECK (reps_done >= 0 AND reps_done <= 100),
    CONSTRAINT unique_set_log UNIQUE(client_id, training_plan_day_exercise_id, set_number)
);

CREATE INDEX idx_logs_client ON client_exercise_set_logs(client_id);
CREATE INDEX idx_logs_exercise ON client_exercise_set_logs(training_plan_day_exercise_id);
```

---

## Tabelas Avaliações

### 19. `initial_assessments`

Avaliação inicial do cliente.

```sql
CREATE TABLE initial_assessments (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    age INTEGER NOT NULL,
    gender VARCHAR(10) NOT NULL, -- 'male', 'female', 'other'
    weight_kg DECIMAL(10, 2) NOT NULL,
    height_cm INTEGER NOT NULL,
    body_fat_percentage DECIMAL(10, 2),
    medical_conditions TEXT,
    fitness_level VARCHAR(50) NOT NULL, -- 'sedentary', 'lightly_active', 'moderately_active', 'very_active'
    goals TEXT NOT NULL,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE
);

CREATE INDEX idx_assessments_trainer ON initial_assessments(owner_trainer_id);
CREATE INDEX idx_assessments_client ON initial_assessments(client_id);
```

### 20. `checkins`

Check-ins periódicos (peso, body fat %).

```sql
CREATE TABLE checkins (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    check_in_date DATE NOT NULL,
    target_date DATE,
    weight_kg DECIMAL(10, 2),
    body_fat_percentage DECIMAL(10, 2),
    notes TEXT,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE
);

CREATE INDEX idx_checkins_trainer ON checkins(owner_trainer_id);
CREATE INDEX idx_checkins_client ON checkins(client_id);
CREATE INDEX idx_checkins_date ON checkins(check_in_date);
```

---

## Tabelas Sessões

### 21. `sessions`

Sessões com trainer.

```sql
CREATE TABLE sessions (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    session_date DATE NOT NULL,
    session_time TIME,
    duration_minutes INTEGER,
    session_type VARCHAR(50), -- 'strength', 'cardio', 'flexibility', 'assessment'
    notes TEXT,
    is_completed BOOLEAN DEFAULT false,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE
);

CREATE INDEX idx_sessions_trainer ON sessions(owner_trainer_id);
CREATE INDEX idx_sessions_client ON sessions(client_id);
CREATE INDEX idx_sessions_date ON sessions(session_date);
```

---

## Tabelas Billing

### 22. `pack_types`

Tipos de packs (sessões).

```sql
CREATE TABLE pack_types (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID, -- NULL = global
    name VARCHAR(255) NOT NULL,
    session_count INTEGER NOT NULL,
    price_cents INTEGER NOT NULL,
    duration_days INTEGER,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_packs_trainer ON pack_types(owner_trainer_id);
```

### 23. `client_session_packs`

Packs comprados por cliente.

```sql
CREATE TABLE client_session_packs (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    pack_type_id UUID NOT NULL,
    sessions_remaining INTEGER NOT NULL,
    purchase_date DATE NOT NULL,
    expiry_date DATE,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE,
    CONSTRAINT fk_pack_type FOREIGN KEY (pack_type_id) REFERENCES pack_types(id) ON DELETE CASCADE
);

CREATE INDEX idx_client_packs_trainer ON client_session_packs(owner_trainer_id);
CREATE INDEX idx_client_packs_client ON client_session_packs(client_id);
```

### 24. `processed_stripe_events`

Idempotência dos webhooks Stripe (`00_ARCHITECTURE.md §10.2`) — deduplica por `event.id`.

```sql
CREATE TABLE processed_stripe_events (
    id UUID PRIMARY KEY,
    stripe_event_id VARCHAR(255) NOT NULL UNIQUE,
    event_type VARCHAR(100) NOT NULL,
    processed_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_stripe_events_type ON processed_stripe_events(event_type);
```

---

## Tabelas Notificações

### 25. `notifications`

Histórico de notificações.

```sql
CREATE TABLE notifications (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID,
    recipient_email VARCHAR(255) NOT NULL,
    notification_type VARCHAR(50) NOT NULL, -- 'session_reminder', 'welcome', 'receipt', etc
    template_key VARCHAR(255) NOT NULL,
    template_data JSONB, -- {'client_name': 'John', 'date': '2025-01-15'}
    status VARCHAR(50) DEFAULT 'pending', -- 'pending', 'sent', 'failed', 'bounced'
    retry_count INTEGER DEFAULT 0,
    last_retry_at TIMESTAMPTZ,
    error_message TEXT,
    sent_at TIMESTAMPTZ,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE SET NULL,
    CONSTRAINT status_check CHECK (status IN ('pending', 'sent', 'failed', 'bounced'))
);

CREATE INDEX idx_notifications_trainer ON notifications(owner_trainer_id);
CREATE INDEX idx_notifications_status ON notifications(status);
CREATE INDEX idx_notifications_created ON notifications(created_at);
```

---

## Tabelas Jobs & Outbox (novo em v3.0)

Substituem RabbitMQ/MassTransit — cobrem `00_ARCHITECTURE.md §9` (jobs duráveis via QStash) e `§10.3` (outbox transacional do Stripe).

### 26. `durable_jobs`

Fila de jobs processada pelo dispatcher interno, ativado pelo QStash a cada vinte minutos (`00_ARCHITECTURE.md §9.1`).

```sql
CREATE TABLE durable_jobs (
    id UUID PRIMARY KEY,
    trainer_id UUID, -- NULL quando o job não é tenant-owned
    job_type VARCHAR(100) NOT NULL, -- 'send_notification', 'process_billing', etc
    job_version INTEGER NOT NULL DEFAULT 1,
    payload JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'pending', -- 'pending', 'processing', 'completed', 'failed', 'dead_letter'
    scheduled_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    attempts INTEGER NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMPTZ,
    lease_expires_at TIMESTAMPTZ, -- evita processamento duplicado concorrente
    idempotency_key VARCHAR(255) NOT NULL,
    correlation_id UUID NOT NULL,
    last_error TEXT, -- mensagem sanitizada, nunca stack trace completo
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT status_check CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')),
    CONSTRAINT unique_idempotency_key UNIQUE (idempotency_key)
);

CREATE INDEX idx_jobs_status_scheduled ON durable_jobs(status, scheduled_at) WHERE status IN ('pending', 'processing');
CREATE INDEX idx_jobs_trainer ON durable_jobs(trainer_id);
CREATE INDEX idx_jobs_lease ON durable_jobs(lease_expires_at) WHERE status = 'processing';
```

Reclamação transacional de jobs vencidos (dispatcher, `00_ARCHITECTURE.md §9.4`):

```sql
UPDATE durable_jobs
SET status = 'processing', lease_expires_at = now() + INTERVAL '2 minutes', attempts = attempts + 1
WHERE id IN (
    SELECT id FROM durable_jobs
    WHERE status = 'pending' AND scheduled_at <= now()
       OR (status = 'processing' AND lease_expires_at < now())
    ORDER BY scheduled_at
    LIMIT 20
    FOR UPDATE SKIP LOCKED
)
RETURNING *;
```

### 27. `outbox_messages`

Liga alterações PostgreSQL a efeitos secundários sem transação distribuída (email pós-pagamento, notificações internas).

```sql
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    trainer_id UUID,
    message_type VARCHAR(100) NOT NULL, -- 'payment_confirmed_email', 'payment_failed_alert', etc
    payload JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'pending', -- 'pending', 'dispatched', 'completed', 'failed'
    correlation_id UUID NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    dispatched_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT status_check CHECK (status IN ('pending', 'dispatched', 'completed', 'failed'))
);

CREATE INDEX idx_outbox_status ON outbox_messages(status) WHERE status = 'pending';
CREATE INDEX idx_outbox_trainer ON outbox_messages(trainer_id);
```

Um item de outbox é escrito **na mesma transação** que a alteração de domínio que o originou (ex.: `processed_stripe_events` + `outbox_messages` no mesmo `SaveChanges`). O dispatcher de jobs entrega os itens pendentes de forma idempotente; um item só passa a `completed` depois do efeito (ex. email enviado) ser confirmado.

---

## Tabelas Infrastructure

### 28. `__EFMigrationsHistory`

Tracking de migrations, criada e gerida automaticamente pelo EF Core (substitui o `schema_migrations` manual do Python — não é criada à mão).

---

## Constraints Globais

### Foreign Keys

Todas as FKs com `ON DELETE CASCADE` ou `ON DELETE SET NULL` conforme necessário.

### Indexes Críticos

```sql
-- Multi-tenancy
CREATE INDEX idx_clients_trainer_active ON clients(owner_trainer_id, is_deleted);
CREATE INDEX idx_meal_plans_trainer_active ON meal_plans(owner_trainer_id, is_active);
CREATE INDEX idx_training_plans_trainer_active ON training_plans(owner_trainer_id, is_active);

-- Timestamps (para limpeza de dados antigos)
CREATE INDEX idx_notifications_old ON notifications(created_at) WHERE created_at < now() - INTERVAL '6 months';

-- Search
CREATE INDEX idx_foods_search ON foods USING GIN(to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
CREATE INDEX idx_exercises_search ON exercises USING GIN(to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
```

---

## Geração de Migrations (EF Core)

Este documento é a spec; a migration real é gerada, nunca escrita manualmente:

```bash
# Depois do modelo EF Core (entities + Fluent API) estar pronto
dotnet ef migrations add InitialCreate --project src/Infrastructure

# Aplicar contra PostgreSQL local ou Neon
dotnet ef database update --project src/Infrastructure
```

Regra herdada do Python que se mantém, adaptada: **nunca editar uma migration EF Core já aplicada num ambiente partilhado** — uma correção é sempre uma migration nova (`dotnet ef migrations add FixX`).

---

## Performance Considerations

### Vacuum & Analyze

```sql
VACUUM ANALYZE;
```

Rodar mensalmente em produção (ou confiar no autovacuum do Neon).

### Statistics

```sql
ANALYZE; -- Atualiza planner statistics
```

### Replication

Neon fornece read replicas automáticas. Configurar connection pooling (`Npgsql` pooling nativo, sem PgBouncer adicional no MVP).

---

*Fim do Schema Database v3.0*
