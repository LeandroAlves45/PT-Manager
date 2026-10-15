# Modelo de dados — o que não se copia

Estado: mapa de leitura em 2026-10-15. A fonte é `.claude/project/01_DATABASE_SCHEMA.md`.

## Mapa das tabelas

| Grupo | Tabelas já descritas no esquema |
|---|---|
| Core | `trainer_subscriptions`, `clients`, `trainer_settings`, `refresh_tokens`, `invite_tokens`, `email_verification_tokens`, `password_reset_tokens`, `tenant_transfer_audits` |
| Nutrição | `foods`, `supplements`, `meal_plans`, `meal_plan_meals`, `meal_plan_meal_items`, `meal_plan_meal_supplements` |
| Treino | `training_plans`, `training_plan_days`, `exercises`, `training_plan_day_exercises`, `exercise_sets`, `client_exercise_set_logs` |
| Avaliações | `initial_assessments`, `checkins`, `client_supplement_assignments` |
| Sessões e billing | `sessions`, `billing_checkout_operations`, `pack_types`, `client_session_packs`, `processed_stripe_events` |
| Resto | `notifications`, `durable_jobs`, `outbox_messages`, `qstash_dispatch_receipts`, `administrative_audit_entries` |

Identificadores são `uuid`. A conta está em `users`, não no esquema padrão do ASP.NET Core Identity. Apagar segue a `RN-017`: quase tudo é soft delete, com as excepções já escritas nessa regra.

## O que a entrevista pede e o esquema ainda não tem

| Entidade ou relação | O que o esquema tem hoje | O que a entrevista já decidiu |
|---|---|---|
| `exercise_sets.planned_reps` | Um inteiro, ou vazio | Na 6GB-2 a série pode ter máximo opcional. Um só número é o alvo. Com os dois, escreve-se o maior primeiro, como 12-10. Ainda não há coluna de máximo. |
| `client_exercise_set_logs.reps_done` | Um inteiro, de 0 a 100 | O cliente regista um só número, mesmo fora do intervalo. A app aceita e não avisa. Cabe na coluna actual. |
| `supplements.timing` | Obrigatório e não vazio (`ck_supplements_timing`) | Momento pode ficar vazio. Decisão de 2026-10-15. O esquema ainda não o permite. |
| `supplements.unit_of_measure` | Texto livre, não vazio | «gr» guarda-se como «g». Não pede coluna nova. Um campo numérico de gramas não aceita «5gr». |
| `invite_tokens` | `email`, `expires_at`, `used_at`, e só o hash do token | A ficha da 6GB-2 lê estes campos: válido até à data, expirado com a data, aceite deixa de mostrar, email antigo continua no convite. O link em claro não se reconstrói a partir do hash. |
| `client_supplement_assignments` | Atribuição por cliente | Cliente activo aparece na lista. Cliente arquivado sai da lista activa e os dados ficam. |
| Dinheiro do mês no painel | `billing_checkout_operations`, `processed_stripe_events`, `trainer_subscriptions` | `RN-018`: só pagamentos confirmados. Checkout por pagar não entra. Não há tabela desse total. |
| Marca | `trainer_settings` | Aplicar a marca a todas as páginas da área do treinador não pede tabela nova. Entra na 6GB-2. |
