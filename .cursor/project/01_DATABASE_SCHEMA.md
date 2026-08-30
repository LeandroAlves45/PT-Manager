# PT Manager — Database Schema v3.0 (PostgreSQL 17 / EF Core 10)

*Schema Definition — Julho 2026*

Estado: o baseline foi materializado por `20260804163659_InitialCreate` e
completado por `20260814121132_CompleteTrainingPhase2C`. O delta dos Lotes 3A a
3E foi consolidado por `20260822155532_CompleteSprint3Phase3` e validado em
PostgreSQL 17 descartável. O snapshot representa o modelo implementado atual e
não existem alterações pendentes. O Gate 3G-A aprovou como alvo futuro a
substituição de `uq_clients_user` por unicidade parcial da relação ativa; essa
alteração ainda não foi implementada nem migrada. A migration consolidada não
foi aplicada a uma base persistente.

---

## Overview

Este documento é a **especificação do modelo EF Core alvo**, não um script
SQL a correr manualmente. `InitialCreate` e `CompleteTrainingPhase2C` são o
baseline imutável; o delta atual foi gerado como `CompleteSprint3Phase3`. O
histórico de migrations SQL do Python não é convertido. Embora não exista uma
base de produção identificada, qualquer base .NET persistente pode conter dados
e deve seguir o preflight e a política de backup deste documento.

**Características:**
- Contagem: 32 tabelas da aplicação mais `__EFMigrationsHistory`, total 33
- Multi-tenancy: raízes com `owner_trainer_id`; filhas herdam o tenant por navegação para a raiz. Filtros centralizados no `DbContext`, ligados a `ITenantContext`, exigem tenant presente
- IDs: `uuid` nativo (ver decisão §1 abaixo)
- Soft delete: `is_deleted` apenas nas entidades que ainda distinguem remoção
  interna. `foods`, `exercises`, `supplements` e
  `client_supplement_assignments` usam disponibilidade por `is_active`
- Timestamps: `created_at`, `updated_at` em UTC
- Constraints: FK cascades, unique indexes
- Generated columns: `kcal` calculado automaticamente
- Pesquisa: `pg_trgm` e índices GIN trigram para os filtros `ILIKE` de Food,
  Exercise e Supplement
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

Estado: desenho reservado ao Sprint 4. O Lote 3F não altera `users`, não cria
logins externos e mantém `password_hash` obrigatório.

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
    subscription_status VARCHAR(50) NOT NULL DEFAULT 'ACTIVE', -- ACTIVE, INACTIVE, SUSPENDED, CANCELLED
    subscription_tier VARCHAR(50) NOT NULL DEFAULT 'FREE', -- FREE, STARTER, PRO
    client_limit INTEGER NOT NULL DEFAULT 5,
    current_client_count INTEGER DEFAULT 0,
    is_exempt_from_billing BOOLEAN DEFAULT false,
    trial_ends_at TIMESTAMPTZ,
    stripe_subscription_id VARCHAR(255),
    stripe_customer_id VARCHAR(255),
    last_provider_state_observed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT uq_trainer_subscriptions_trainer UNIQUE (trainer_id),
    CONSTRAINT status_check CHECK (subscription_status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED', 'CANCELLED')),
    CONSTRAINT tier_check CHECK (subscription_tier IN ('FREE', 'STARTER', 'PRO'))
);

CREATE INDEX idx_subscriptions_trainer ON trainer_subscriptions(trainer_id);
CREATE INDEX idx_subscriptions_status ON trainer_subscriptions(subscription_status);
CREATE UNIQUE INDEX uq_trainer_subscriptions_stripe_customer
    ON trainer_subscriptions(stripe_customer_id)
    WHERE stripe_customer_id IS NOT NULL;
CREATE UNIQUE INDEX uq_trainer_subscriptions_stripe_subscription
    ON trainer_subscriptions(stripe_subscription_id)
    WHERE stripe_subscription_id IS NOT NULL;
```

### 3. `clients`

Clientes do trainer (multi-tenant).

```sql
CREATE TABLE clients (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    user_id UUID,
    name VARCHAR(255) NOT NULL,
    contact_email VARCHAR(255),
    normalized_contact_email VARCHAR(255),
    phone VARCHAR(32) NOT NULL,
    date_of_birth DATE NOT NULL,
    sex VARCHAR(6) NOT NULL,
    objective VARCHAR(255),
    notes TEXT,
    emergency_contact_name VARCHAR(255),
    emergency_contact_phone VARCHAR(32),
    avatar_url VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_clients_sex CHECK (sex IN ('male', 'female')),
    CONSTRAINT fk_clients_owner_trainer
        FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_clients_user
        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL,
    CONSTRAINT uq_clients_tenant_id UNIQUE (owner_trainer_id, id)
);

CREATE INDEX idx_clients_owner_trainer ON clients(owner_trainer_id);
-- permite relações históricas, mantendo uma única relação ativa por conta.
CREATE UNIQUE INDEX uq_clients_user_active ON clients(user_id)
    WHERE user_id IS NOT NULL AND is_active = true AND is_deleted = false;
CREATE UNIQUE INDEX uq_clients_tenant_contact_email_active
    ON clients(owner_trainer_id, normalized_contact_email)
    WHERE normalized_contact_email IS NOT NULL AND is_deleted = false;
CREATE UNIQUE INDEX uq_clients_tenant_phone_active
    ON clients(owner_trainer_id, phone)
    WHERE is_deleted = false;
```

### 4. `trainer_settings`

Branding + configurações por trainer.

```sql
CREATE TABLE trainer_settings (
    id UUID PRIMARY KEY,
    trainer_id UUID NOT NULL UNIQUE,
    app_name VARCHAR(50) NOT NULL DEFAULT 'PT Manager',
    logo_url VARCHAR(500), -- NULL: frontend usa o asset padrão do PT Manager
    logo_public_id VARCHAR(500), -- NULL sem media personalizado do trainer
    primary_color VARCHAR(7), -- NULL: frontend usa a cor padrão
    body_color VARCHAR(7), -- NULL: frontend usa a cor padrão
    phone VARCHAR(20),
    address VARCHAR(500),
    city VARCHAR(255),
    time_zone_id VARCHAR(100) NOT NULL DEFAULT 'Europe/Lisbon',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_settings_trainer ON trainer_settings(trainer_id);
```

`logo_url` nunca recebe a localização do asset padrão da aplicação. Apenas
media personalizado do trainer ocupa `logo_url` e `logo_public_id`. Isto mantém
o asset global fora do ciclo Cloudinary, RemoveLogo e outbox.

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
    client_id UUID NOT NULL,
    email VARCHAR(255) NOT NULL,
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_invite_client_tenant FOREIGN KEY (trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE
);

CREATE INDEX idx_invites_trainer ON invite_tokens(trainer_id);
CREATE INDEX idx_invites_email ON invite_tokens(email);
CREATE INDEX idx_invites_client ON invite_tokens(client_id);
```

### 6A. `email_verification_tokens`

Tokens de utilização única para confirmação de email. Apenas o hash SHA-256 é
persistido.

```sql
CREATE TABLE email_verification_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    consumed_at TIMESTAMPTZ,

    CONSTRAINT fk_email_verification_tokens_user
        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT uq_email_verification_tokens_hash UNIQUE (token_hash)
);

CREATE INDEX idx_email_verification_tokens_user_consumed
    ON email_verification_tokens(user_id, consumed_at);
```

### 6B. `password_reset_tokens`

Tokens de utilização única para recuperação de password. Apenas o hash SHA-256
é persistido.

```sql
CREATE TABLE password_reset_tokens (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    token_hash VARCHAR(64) NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    consumed_at TIMESTAMPTZ,

    CONSTRAINT fk_password_reset_tokens_user
        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT uq_password_reset_tokens_hash UNIQUE (token_hash)
);

CREATE INDEX idx_password_reset_tokens_user_consumed
    ON password_reset_tokens(user_id, consumed_at);
```

### 6C. `tenant_transfer_audits`

Auditoria append-only da transferência explícita de uma conta de cliente entre
tenants.

```sql
CREATE TABLE tenant_transfer_audits (
    id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    source_trainer_id UUID NOT NULL,
    target_trainer_id UUID NOT NULL,
    target_client_id UUID NOT NULL,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_tenant_transfer_audits_user
        FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tenant_transfer_audits_source_trainer
        FOREIGN KEY (source_trainer_id) REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tenant_transfer_audits_target_trainer
        FOREIGN KEY (target_trainer_id) REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT fk_tenant_transfer_audits_target_client_tenant
        FOREIGN KEY (target_trainer_id, target_client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE RESTRICT
);

CREATE INDEX idx_tenant_transfer_audits_user_occurred
    ON tenant_transfer_audits(user_id, occurred_at);
```

---

## Tabelas Nutrição

### 7. `foods`

Catálogo global ou privado de alimentos. `owner_trainer_id = NULL` identifica
uma entrada global; um UUID identifica o trainer proprietário.

```sql
CREATE TABLE foods (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID, -- NULL = global catalogue
    name VARCHAR(255) NOT NULL,
    description TEXT,
    protein DECIMAL(10, 2) NOT NULL,
    carbs DECIMAL(10, 2) NOT NULL,
    fats DECIMAL(10, 2) NOT NULL,
    kcal DECIMAL(10, 2) GENERATED ALWAYS AS (
        protein * 4 + carbs * 4 + fats * 9
    ) STORED,
    fiber DECIMAL(10, 2),
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_foods_owner_trainer FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT ck_foods_nutrients_per_100g CHECK (
        protein BETWEEN 0 AND 100
        AND carbs BETWEEN 0 AND 100
        AND fats BETWEEN 0 AND 100
        AND protein + carbs + fats <= 100
        AND (fiber IS NULL OR fiber >= 0))
);

CREATE INDEX idx_foods_owner_name ON foods(owner_trainer_id, name);
CREATE INDEX idx_foods_search ON foods USING GIN(
    to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
CREATE INDEX idx_foods_search_trgm ON foods USING GIN(
    description gin_trgm_ops, name gin_trgm_ops);
```

O Lote 3F remove `is_deleted` depois de converter linhas antigas apagadas para
`is_active = false`. `PlatformEnforcementStatus`, motivo e timestamp pertencem
ao vertical slice de moderação do Sprint 4B e exigem uma migration nova; não
fazem parte de `CompleteSprint3Phase3`.

### 8. `supplements`

Suplementos globais ou privados. `owner_trainer_id` define propriedade;
`created_by_user_id` regista apenas autoria.

```sql
CREATE TABLE supplements (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID, -- NULL = seed global ou criação global autorizada
    created_by_user_id UUID NOT NULL, -- autoria, não autorização
    name VARCHAR(255) NOT NULL,
    description TEXT,
    serving_size VARCHAR(100) NOT NULL,
    timing VARCHAR(255) NOT NULL,
    trainer_notes TEXT,
    unit_of_measure VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_supplements_owner_trainer FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_supplements_created_by_user FOREIGN KEY (created_by_user_id)
        REFERENCES users(id) ON DELETE RESTRICT,
    CONSTRAINT ck_supplements_name CHECK (btrim(name) <> ''),
    CONSTRAINT ck_supplements_unit CHECK (btrim(unit_of_measure) <> ''),
    CONSTRAINT ck_supplements_serving_size CHECK (btrim(serving_size) <> ''),
    CONSTRAINT ck_supplements_timing CHECK (btrim(timing) <> '')
);

CREATE INDEX idx_supplements_scope_active_name_id
    ON supplements(owner_trainer_id, is_active, name, id);
CREATE INDEX idx_supplements_search_trgm ON supplements USING GIN(
    description gin_trgm_ops, name gin_trgm_ops);
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
    fats_target_g DECIMAL(10, 2) NOT NULL,
    kcal_target DECIMAL(10, 2) NOT NULL,
    calculation_snapshot JSONB NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_archived BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_meal_plans_owner_trainer
        FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_meal_plans_client_tenant
        FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    -- ends_date pode ser NULL (plano sem data de fim); a comparação com NULL
    -- avalia a UNKNOWN em Postgres, e uma CHECK só rejeita quando avalia a
    -- FALSE — logo esta constraint continua correta sem tratamento especial.
    CONSTRAINT ck_meal_plans_date_order CHECK (starts_date <= ends_date),
    CONSTRAINT ck_meal_plans_targets CHECK (
        kcal_target > 0
        AND protein_target_g >= 0
        AND carbs_target_g >= 0
        AND fats_target_g >= 0
        AND abs((protein_target_g * 4 + carbs_target_g * 4
                 + fats_target_g * 9) - kcal_target) <= 100)
);

CREATE INDEX idx_meal_plans_trainer ON meal_plans(owner_trainer_id);
CREATE INDEX idx_meal_plans_client ON meal_plans(client_id);
CREATE INDEX idx_meal_plans_trainer_active
    ON meal_plans(owner_trainer_id, is_active);
```

`calculation_snapshot` guarda a entrada e o resultado imutáveis usados para
calcular os alvos do plano. A versão inicial usa `schema_version = 1` e as
seguintes chaves JSON em `snake_case`:

```text
schema_version, calculation_origin, calculated_at, energy_formula,
weight_kg_used, height_cm_used, age_used, sex_used,
body_fat_percentage_used, activity_level, activity_factor, goal_type,
goal_adjustment_kcal, resting_energy_expenditure_kcal,
total_daily_energy_expenditure_kcal, target_kcal,
macro_distribution_mode, protein_percentage_input,
carbs_percentage_input, fats_percentage_input,
protein_grams_per_kg_input, fats_grams_per_kg_input,
protein_target_grams, carbs_target_grams, fats_target_grams,
protein_energy_percentage, carbs_energy_percentage,
fats_energy_percentage, calculated_macro_kcal, kcal_difference
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
    CONSTRAINT meal_order_positive CHECK (order_number > 0),
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
    CONSTRAINT fk_food FOREIGN KEY (food_id) REFERENCES foods(id) ON DELETE RESTRICT,
    CONSTRAINT positive_quantity CHECK (quantity_grams > 0),
    CONSTRAINT meal_item_order_positive CHECK (order_number > 0)
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
    CONSTRAINT fk_supplement FOREIGN KEY (supplement_id) REFERENCES supplements(id) ON DELETE RESTRICT,
    CONSTRAINT unique_supplement_per_meal UNIQUE(meal_plan_meal_id, supplement_id),
    CONSTRAINT positive_supplement_quantity CHECK (quantity > 0),
    CONSTRAINT meal_supplement_order_positive CHECK (order_number > 0)
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
    CONSTRAINT fk_client_tenant FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT date_order CHECK (starts_date <= ends_date)
);

CREATE INDEX idx_training_plans_trainer ON training_plans(owner_trainer_id);
CREATE INDEX idx_training_plans_client ON training_plans(client_id);
CREATE UNIQUE INDEX uq_training_plan_active_per_client
    ON training_plans(owner_trainer_id, client_id)
    WHERE is_active = true AND is_deleted = false;
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
CREATE UNIQUE INDEX uq_training_plan_day_weekday
    ON training_plan_days(training_plan_id, week_number, day_of_week);
```

### 15. `exercises`

Catálogo global ou privado de exercícios. `owner_trainer_id = NULL` identifica
uma entrada global; um UUID identifica o trainer proprietário.

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
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT "FK_exercises_users_owner_trainer_id" FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX idx_exercises_name ON exercises(name);
CREATE INDEX idx_exercises_trainer ON exercises(owner_trainer_id);
CREATE INDEX idx_exercises_search ON exercises USING GIN(
    to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
CREATE INDEX idx_exercises_search_trgm ON exercises USING GIN(
    description gin_trgm_ops, name gin_trgm_ops);
```

O Lote 3F remove `is_deleted` depois de preservar arquivo em
`is_active = false`. As colunas e constraints de enforcement privado são
adicionadas apenas no Sprint 4B, com testes e migration próprios.

### 16. `training_plan_day_exercises`

Exercícios dum dia de treino.

```sql
CREATE TABLE training_plan_day_exercises (
    id UUID PRIMARY KEY,
    training_plan_day_id UUID NOT NULL,
    exercise_id UUID NOT NULL,
    order_number INTEGER NOT NULL,
    exercise_group_id UUID,
    group_position INTEGER,
    notes TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_day FOREIGN KEY (training_plan_day_id) REFERENCES training_plan_days(id) ON DELETE CASCADE,
    CONSTRAINT fk_exercise FOREIGN KEY (exercise_id) REFERENCES exercises(id) ON DELETE RESTRICT,
    CONSTRAINT day_exercise_order_positive CHECK (order_number > 0),
    CONSTRAINT day_exercise_group_consistency CHECK (
        (exercise_group_id IS NULL AND group_position IS NULL)
        OR (exercise_group_id IS NOT NULL AND group_position > 0)
    )
);

CREATE INDEX idx_day_exercises_day ON training_plan_day_exercises(training_plan_day_id);
CREATE UNIQUE INDEX uq_day_exercise_isolated_order
    ON training_plan_day_exercises(training_plan_day_id, order_number)
    WHERE exercise_group_id IS NULL;
CREATE UNIQUE INDEX uq_day_exercise_group_position
    ON training_plan_day_exercises(training_plan_day_id, exercise_group_id, group_position)
    WHERE exercise_group_id IS NOT NULL;
```

O Domain e o interceptor garantem que todas as linhas do mesmo
`exercise_group_id` partilham `order_number`. Sem tabela de grupo, esta regra
cross-row não é exprimível por uma `CHECK` row-local; os índices acima garantem
as restantes invariantes sem introduzir trigger.

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
    CONSTRAINT planned_weight_check CHECK (planned_weight_kg IS NULL OR planned_weight_kg >= 0),
    CONSTRAINT rest_min_check CHECK (rest_seconds_min IS NULL OR rest_seconds_min >= 0),
    CONSTRAINT rest_max_check CHECK (rest_seconds_max IS NULL OR rest_seconds_max >= 0),
    CONSTRAINT rest_range_check CHECK (
        rest_seconds_min IS NULL OR rest_seconds_max IS NULL OR rest_seconds_min <= rest_seconds_max
    )
);

CREATE INDEX idx_sets_exercise ON exercise_sets(training_plan_day_exercise_id);
CREATE UNIQUE INDEX uq_exercise_set_number
    ON exercise_sets(training_plan_day_exercise_id, set_number);
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
    performed_at TIMESTAMPTZ NOT NULL DEFAULT now(), -- Official timestamp (not created_at)
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_client FOREIGN KEY (client_id) REFERENCES clients(id) ON DELETE CASCADE,
    CONSTRAINT fk_day_exercise FOREIGN KEY (training_plan_day_exercise_id)
        REFERENCES training_plan_day_exercises(id) ON DELETE RESTRICT,
    CONSTRAINT set_num_check CHECK (set_number >= 1 AND set_number <= 15),
    CONSTRAINT weight_check CHECK (weight_kg >= 0),
    CONSTRAINT reps_check CHECK (reps_done >= 0 AND reps_done <= 100)
);

CREATE INDEX idx_logs_client_performed_at
    ON client_exercise_set_logs(client_id, performed_at DESC, id);
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
    weight_kg DECIMAL(10, 2) NOT NULL,
    height_cm INTEGER NOT NULL,
    body_fat_percentage DECIMAL(10, 2),
    medical_conditions TEXT,
    fitness_level VARCHAR(50) NOT NULL,
    activity_level VARCHAR(32) NOT NULL,
    goals TEXT NOT NULL,
    profession VARCHAR(255),
    body_measurements JSONB NOT NULL,
    nutrition_intake JSONB NOT NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_initial_assessments_owner_trainer
        FOREIGN KEY (owner_trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_initial_assessments_client_tenant
        FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT ck_initial_assessments_weight_positive CHECK (weight_kg > 0),
    CONSTRAINT ck_initial_assessments_height_positive CHECK (height_cm > 0),
    CONSTRAINT ck_initial_assessments_body_fat_range CHECK (
        body_fat_percentage IS NULL OR
        (body_fat_percentage > 0 AND body_fat_percentage < 100)),
    CONSTRAINT ck_initial_assessments_activity_level CHECK (
        activity_level IN ('sedentary', 'lightly_active', 'moderately_active',
                           'very_active', 'extremely_active'))
);

CREATE INDEX idx_initial_assessments_trainer
    ON initial_assessments(owner_trainer_id);
CREATE UNIQUE INDEX uq_initial_assessments_tenant_client_active
    ON initial_assessments(owner_trainer_id, client_id)
    WHERE is_deleted = false;
```

**Estrutura de `nutrition_intake` (JSONB, todas as chaves opcionais):**

```json
{
    "food_preferences": "...",
    "disliked_foods": "...",
    "food_intolerances": "...",
    "food_allergies": "...",
    "dietary_restrictions": "...",
    "daily_routine": "...",
    "sleep_quality": 4,
    "mood": 4,
    "stress_level": 2,
    "avg_water_liters_per_day": 2.5,
    "hungriest_time_of_day": "...",
    "uses_supplements": true,
    "current_supplements": "...",
    "other_notes": "..."
}
```

**Estrutura de `body_measurements` (JSONB, todas as chaves opcionais):**

```json
{
    "waist_cm": 80.0,
    "hip_cm": 95.0,
    "chest_cm": 100.0,
    "right_arm_cm": 35.0,
    "left_arm_cm": 35.0,
    "right_thigh_cm": 55.0,
    "left_thigh_cm": 55.0,
    "right_calf_cm": 38.0,
    "left_calf_cm": 38.0
}
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
    body_measurements JSONB NOT NULL,
    feedback JSONB NOT NULL,
    training_adherence_score INTEGER,
    nutrition_adherence_score INTEGER,
    responded_at TIMESTAMPTZ,
    cancelled_at TIMESTAMPTZ,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT "FK_checkins_users_owner_trainer_id" FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_checkins_client_tenant FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT ck_checkins_date_order CHECK (
        target_date IS NULL OR target_date >= check_in_date),
    CONSTRAINT ck_checkins_weight_positive CHECK (
        weight_kg IS NULL OR weight_kg > 0),
    CONSTRAINT ck_checkins_body_fat_range CHECK (
        body_fat_percentage IS NULL OR
        (body_fat_percentage > 0 AND body_fat_percentage < 100)),
    CONSTRAINT ck_checkins_training_adherence_range CHECK (
        training_adherence_score IS NULL OR
        training_adherence_score BETWEEN 0 AND 100),
    CONSTRAINT ck_checkins_nutrition_adherence_range CHECK (
        nutrition_adherence_score IS NULL OR
        nutrition_adherence_score BETWEEN 0 AND 100),
    CONSTRAINT ck_checkins_single_terminal_event CHECK (
        NOT (responded_at IS NOT NULL AND cancelled_at IS NOT NULL)),
    CONSTRAINT ck_checkins_response_requires_weight CHECK (
        responded_at IS NULL OR weight_kg IS NOT NULL)
);

CREATE INDEX idx_checkins_trainer ON checkins(owner_trainer_id);
CREATE UNIQUE INDEX uq_checkins_tenant_client_date_active
    ON checkins(owner_trainer_id, client_id, check_in_date)
    WHERE is_deleted = false;
CREATE INDEX idx_checkins_tenant_date_id
    ON checkins(owner_trainer_id, check_in_date, id);
```

**Estrutura de `feedback` (JSONB, todas as chaves opcionais):**

```json
{
    "appetite": "...",        // "Como está o apetite? Fome ou a empurrar comida?"
    "digestion": "...",       // "Trânsito intestinal ok? Algum alimento a fazer mal?"
    "training_load": "...",   // "Nível de rendimento/cargas no treino, a aumentar?"
    "recovery_sleep": "...",  // "Recuperação muscular / qualidade do sono?"
    "energy_levels": "...",   // "Níveis de energia?"
    "body_response": "..."    // "Como sentes que o corpo está a responder?"
}
```

O "peso anterior" pedido no acompanhamento não gera coluna nova: obtém-se com
`LAG(weight_kg) OVER (PARTITION BY client_id ORDER BY check_in_date)` na query
de histórico, evitando duplicar um dado que já existe na linha do checkin
anterior.

### 21. `client_supplement_assignments`

Atribuição direta de um suplemento a um cliente, independente dos suplementos associados a refeições.

```sql
CREATE TABLE client_supplement_assignments (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    supplement_id UUID NOT NULL,
    serving_size VARCHAR(100) NOT NULL,
    timing VARCHAR(255) NOT NULL,
    trainer_notes TEXT,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_client_supplement_assignments_client_tenant
        FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_client_supplement_assignments_supplement FOREIGN KEY (supplement_id)
        REFERENCES supplements(id) ON DELETE RESTRICT,
    CONSTRAINT ck_client_supplement_assignments_serving_size
        CHECK (btrim(serving_size) <> ''),
    CONSTRAINT ck_client_supplement_assignments_timing
        CHECK (btrim(timing) <> '')
);

CREATE UNIQUE INDEX uq_client_supplement_active
    ON client_supplement_assignments(owner_trainer_id, client_id, supplement_id)
    WHERE is_active = true;
CREATE INDEX idx_client_supplement_assignments_list
    ON client_supplement_assignments(
        owner_trainer_id, client_id, is_active, updated_at, id);
CREATE INDEX idx_client_supplement_assignments_supplement
    ON client_supplement_assignments(supplement_id);
```

---

## Tabelas Sessões

### 22. `sessions`

Sessões com trainer.

```sql
CREATE TABLE sessions (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    starts_at TIMESTAMPTZ NOT NULL,
    duration_minutes INTEGER NOT NULL,
    location VARCHAR(255),
    client_session_pack_id UUID,
    session_type VARCHAR(50), -- 'strength', 'cardio', 'flexibility', 'assessment'
    notes TEXT,
    status VARCHAR(30) NOT NULL,
    status_changed_at TIMESTAMPTZ NOT NULL,
    is_deleted BOOLEAN NOT NULL DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_sessions_owner_trainer FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_sessions_client_tenant FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT ck_sessions_duration CHECK (
        duration_minutes > 0),
    CONSTRAINT ck_sessions_status CHECK (status IN (
        'scheduled', 'completed', 'cancelled_by_trainer', 'cancelled_by_client', 'no_show'))
);

CREATE UNIQUE INDEX uq_sessions_tenant_scheduled_start
    ON sessions(owner_trainer_id, starts_at)
    WHERE status = 'scheduled' AND is_deleted = false;
CREATE INDEX idx_sessions_tenant_client_starts_at
    ON sessions(owner_trainer_id, client_id, starts_at);
CREATE INDEX idx_sessions_client_session_pack
    ON sessions(client_session_pack_id)
    WHERE client_session_pack_id IS NOT NULL;
```

---

## Tabelas Billing

### 23. `pack_types`

Tipos de packs (sessões).

```sql
CREATE TABLE pack_types (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL, -- multi-tenant: cada trainer gere o próprio catálogo, sem packs globais partilhados
    name VARCHAR(255) NOT NULL,
    session_count INTEGER NOT NULL,
    price_cents INTEGER NOT NULL,
    currency VARCHAR(3) NOT NULL DEFAULT 'EUR',
    expected_duration_days INTEGER,
    is_active BOOLEAN NOT NULL DEFAULT true,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_pack_types_owner_trainer FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT "AK_pack_types_owner_trainer_id_id"
        UNIQUE(owner_trainer_id, id),
    CONSTRAINT ck_pack_types_session_count_positive CHECK (session_count > 0),
    CONSTRAINT ck_pack_types_price_non_negative CHECK (price_cents >= 0),
    CONSTRAINT ck_pack_types_expected_duration_positive CHECK (
        expected_duration_days IS NULL OR expected_duration_days > 0)
);

CREATE INDEX idx_pack_types_tenant_name_active ON pack_types(owner_trainer_id, name)
    WHERE is_active = true AND is_deleted = false;
CREATE UNIQUE INDEX uq_pack_types_tenant_id
    ON pack_types(owner_trainer_id, id);
```

### 24. `client_session_packs`

Packs comprados por cliente.

```sql
CREATE TABLE client_session_packs (
    id UUID PRIMARY KEY,
    owner_trainer_id UUID NOT NULL,
    client_id UUID NOT NULL,
    pack_type_id UUID NOT NULL,
    pack_name VARCHAR(255) NOT NULL,
    total_sessions INTEGER NOT NULL,
    price_cents INTEGER NOT NULL,
    currency VARCHAR(3) NOT NULL,
    sessions_remaining INTEGER NOT NULL,
    purchase_date DATE NOT NULL,
    expected_end_date DATE,
    completed_at TIMESTAMPTZ,
    is_deleted BOOLEAN DEFAULT false,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_client_session_packs_owner_trainer FOREIGN KEY (owner_trainer_id)
        REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT fk_client_session_packs_client_tenant FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE CASCADE,
    CONSTRAINT fk_client_session_packs_pack_type_tenant FOREIGN KEY (owner_trainer_id, pack_type_id)
        REFERENCES pack_types(owner_trainer_id, id) ON DELETE RESTRICT,
    CONSTRAINT "AK_client_session_packs_owner_trainer_id_client_id_id"
        UNIQUE(owner_trainer_id, client_id, id),
    CONSTRAINT ck_client_session_packs_balance CHECK (
        total_sessions > 0 AND sessions_remaining >= 0
        AND sessions_remaining <= total_sessions),
    CONSTRAINT ck_client_session_packs_price_non_negative CHECK (price_cents >= 0),
    CONSTRAINT ck_client_session_packs_expected_end_order CHECK (
        expected_end_date IS NULL OR expected_end_date >= purchase_date),
    CONSTRAINT ck_client_session_packs_completion_consistency CHECK (
        (sessions_remaining = 0 AND completed_at IS NOT NULL)
        OR (sessions_remaining > 0 AND completed_at IS NULL))
);

CREATE INDEX idx_client_session_packs_usable_order
    ON client_session_packs(
        owner_trainer_id, client_id, expected_end_date, created_at, id)
    WHERE sessions_remaining > 0 AND is_deleted = false;

-- A FK é adicionada depois de ambas as tabelas existirem. O EF Core ordenará
-- esta operação de forma equivalente na migration gerada.
ALTER TABLE sessions
    ADD CONSTRAINT fk_sessions_client_pack_tenant
    FOREIGN KEY (owner_trainer_id, client_id, client_session_pack_id)
    REFERENCES client_session_packs(owner_trainer_id, client_id, id)
    ON DELETE RESTRICT;
```

### 25. `processed_stripe_events`

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

### 26. `notifications`

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
    CONSTRAINT fk_client_tenant FOREIGN KEY (owner_trainer_id, client_id)
        REFERENCES clients(owner_trainer_id, id) ON DELETE RESTRICT,
    CONSTRAINT status_check CHECK (status IN ('pending', 'sent', 'failed', 'bounced'))
);

CREATE INDEX idx_notifications_trainer ON notifications(owner_trainer_id);
CREATE INDEX idx_notifications_status ON notifications(status);
CREATE INDEX idx_notifications_created ON notifications(created_at);
```

---

## Tabelas Jobs & Outbox (novo em v3.0)

Substituem RabbitMQ/MassTransit — cobrem `00_ARCHITECTURE.md §9` (jobs duráveis via QStash) e `§10.3` (outbox transacional do Stripe).

### 27. `durable_jobs`

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
    lease_owner_id UUID, -- token opaco novo por execução de claim
    idempotency_key VARCHAR(255) NOT NULL,
    correlation_id UUID NOT NULL,
    last_error TEXT, -- mensagem sanitizada, nunca stack trace completo
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT status_check CHECK (status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')),
    CONSTRAINT durable_jobs_attempts_non_negative CHECK (attempts >= 0),
    CONSTRAINT unique_idempotency_key UNIQUE (idempotency_key)
);

CREATE INDEX idx_jobs_first_attempt ON durable_jobs(scheduled_at)
    WHERE status = 'pending' AND next_attempt_at IS NULL;
CREATE INDEX idx_jobs_retry ON durable_jobs(next_attempt_at)
    WHERE status = 'pending' AND next_attempt_at IS NOT NULL;
CREATE INDEX idx_jobs_trainer ON durable_jobs(trainer_id);
CREATE INDEX idx_jobs_lease ON durable_jobs(lease_expires_at) WHERE status = 'processing';
```

Seleção transacional de jobs vencidos (dispatcher, `00_ARCHITECTURE.md §9.4`):

```sql
SELECT * FROM durable_jobs
WHERE (status = 'pending' AND next_attempt_at IS NULL AND scheduled_at <= @now)
   OR (status = 'pending' AND next_attempt_at IS NOT NULL
       AND next_attempt_at <= @now)
   OR (status = 'processing' AND lease_expires_at <= @now)
ORDER BY CASE WHEN next_attempt_at IS NOT NULL
              THEN next_attempt_at ELSE scheduled_at END
LIMIT @batch_size
FOR UPDATE SKIP LOCKED;
```

O repository materializa as linhas, chama `Claim` com um token novo, executa
`SaveChangesAsync` e faz commit na mesma transação curta. Renovação, conclusão e
falha exigem `status = 'processing'`, token correspondente e
`lease_expires_at > @now`.

### 28. `outbox_messages`

Liga alterações PostgreSQL a efeitos secundários sem transação distribuída (email pós-pagamento, notificações internas).

```sql
CREATE TABLE outbox_messages (
    id UUID PRIMARY KEY,
    trainer_id UUID,
    message_type VARCHAR(100) NOT NULL, -- 'payment_confirmed_email', 'payment_failed_alert', etc
    payload JSONB NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    correlation_id UUID NOT NULL,
    idempotency_key VARCHAR(255) NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    next_attempt_at TIMESTAMPTZ,
    lease_owner_id UUID,
    lease_expires_at TIMESTAMPTZ,
    last_error TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_trainer FOREIGN KEY (trainer_id) REFERENCES users(id) ON DELETE CASCADE,
    CONSTRAINT status_check CHECK (
        status IN ('pending', 'processing', 'completed', 'failed', 'dead_letter')),
    CONSTRAINT outbox_attempts_non_negative CHECK (attempts >= 0),
    CONSTRAINT unique_outbox_idempotency_key UNIQUE (idempotency_key)
);

CREATE INDEX idx_outbox_first_attempt ON outbox_messages(created_at)
    WHERE status = 'pending' AND next_attempt_at IS NULL;
CREATE INDEX idx_outbox_retry ON outbox_messages(next_attempt_at)
    WHERE status = 'pending' AND next_attempt_at IS NOT NULL;
CREATE INDEX idx_outbox_lease ON outbox_messages(lease_expires_at)
    WHERE status = 'processing';
CREATE INDEX idx_outbox_trainer ON outbox_messages(trainer_id);
```

Um item de outbox é escrito **na mesma transação** que a alteração de domínio que o originou (ex.: `processed_stripe_events` + `outbox_messages` no mesmo `SaveChanges`). O dispatcher de jobs entrega os itens pendentes de forma idempotente; um item só passa a `completed` depois do efeito (ex. email enviado) ser confirmado.

---

### 29. `administrative_audit_entries`

Auditoria append-only de mutações administrativas. Não existe FK para o recurso
auditado, permitindo que o registo sobreviva ao hard delete de um suplemento.
Os snapshots contêm apenas os campos necessários para explicar a mutação.

```sql
CREATE TABLE administrative_audit_entries (
    id UUID PRIMARY KEY,
    actor_user_id UUID NOT NULL,
    action VARCHAR(50) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id UUID NOT NULL,
    before_state JSONB,
    after_state JSONB,
    occurred_at TIMESTAMPTZ NOT NULL,

    CONSTRAINT ck_administrative_audit_entries_state
        CHECK (before_state IS NOT NULL OR after_state IS NOT NULL)
);

CREATE INDEX idx_administrative_audit_resource_time
    ON administrative_audit_entries(resource_type, resource_id, occurred_at);
CREATE INDEX idx_administrative_audit_actor_time
    ON administrative_audit_entries(actor_user_id, occurred_at);
```

Entradas só podem ser adicionadas num contexto `superuser` com `UserId`
autenticado e `IsAdministrative`. Update e Delete são rejeitados pelo interceptor.

A infraestrutura append-only entra no Lote 3F. A sua utilização para moderação
privada, incluindo ações `food_platform_blocked`, `food_platform_unblocked`,
`exercise_platform_blocked` e `exercise_platform_unblocked`, pertence ao Sprint
4B. Esse vertical slice acrescentará os campos de enforcement, os contratos HTTP
e a gravação transacional da decisão numa migration própria.

---

## Tabelas Infrastructure

### 30. `__EFMigrationsHistory`

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

-- Search
CREATE INDEX idx_foods_search ON foods USING GIN(to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
CREATE INDEX idx_exercises_search ON exercises USING GIN(to_tsvector('portuguese', name || ' ' || COALESCE(description, '')));
CREATE INDEX idx_foods_search_trgm ON foods USING GIN(description gin_trgm_ops, name gin_trgm_ops);
CREATE INDEX idx_exercises_search_trgm ON exercises USING GIN(description gin_trgm_ops, name gin_trgm_ops);
CREATE INDEX idx_supplements_search_trgm ON supplements USING GIN(description gin_trgm_ops, name gin_trgm_ops);
```

---

## Geração de Migrations (EF Core)

Este documento é a especificação. A migration real nasceu do scaffold do EF Core
e não foi escrita integralmente à mão. O comando usado a partir de `backend/`
foi:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add CompleteSprint3Phase3 `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj `
  --output-dir Data/Migrations `
  --configuration Release `
  --no-build
```

Antes de aplicar, confirmar a base alvo, rever `Up`, `Down`, Designer, snapshot e
script SQL. Qualquer base persistente exige backup ou snapshot recuperável. Nunca
editar `InitialCreate` nem `CompleteTrainingPhase2C`.

### Transição consolidada do Lote 3F

O `Up` da nova migration deve executar as transformações antes de remover colunas
ou ativar constraints incompatíveis:

1. Converter `is_deleted = true` em `is_active = false` para Food, Exercise,
   Supplement e ClientSupplementAssignment.
2. Preencher `client_session_packs.completed_at = updated_at` quando
   `sessions_remaining = 0` e `completed_at IS NULL`.
3. Preencher `checkins.responded_at = updated_at` quando `weight_kg IS NOT NULL`
   e `responded_at IS NULL`, sem converter `checkins.is_deleted` em cancelamento.
4. Preencher `supplements.created_by_user_id = owner_trainer_id` apenas para
   suplementos privados sem autor.
5. Abortar perante suplementos globais sem autor, UUID vazio como autoria,
   `app_name` com mais de 50 caracteres, body fat igual a 0 ou 100, campos
   obrigatórios de Supplement vazios, sessões agendadas duplicadas ou violações
   das novas constraints.

O `Down` deve recuperar a estrutura de `CompleteTrainingPhase2C`, mas não consegue
reconstruir os valores originais de colunas removidas. O backup é a recuperação
integral dos dados.

Regra obrigatória: nunca editar uma migration EF Core já aplicada num ambiente
partilhado. Uma correção é sempre uma migration nova.

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
