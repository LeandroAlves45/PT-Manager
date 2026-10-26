# Arquitectura — o que não se copia

Estado: mapa de leitura em 2026-10-15. A fonte continua a ser `.claude/project/00_ARCHITECTURE.md`. Este ficheiro não a reescreve.

## Mapa de leitura

| Secção | O que já está decidido lá |
|---|---|
| 2 | Monólito modular, Clean Architecture, features, sem `IRepository<T>`, MediatR, AutoMapper nem Unit of Work genérico |
| 4 | Prefixo `/api/v1`, JSON em `snake_case`, Problem Details, `CancellationToken` |
| 5 | ASP.NET Core Identity, JWT curto em memória, refresh opaco em cookie `HttpOnly` |
| 6 | Tenant pelo utilizador autenticado, filtros globais, jobs e webhooks com trainer explícito |
| 7 | PostgreSQL, migrations EF Core novas, sem migração automática no arranque |
| 8 | Sem Redis, cache nem rate limiting distribuído no MVP: o Gate 7B decidiu `Não implementar ainda` (ADR-002, `docs/blueprints/sprint_7/sprint_7B/01_decisao_gate_7B.md`). Reavaliar no 10B com tráfego real |
| 9 | Jobs e outbox em PostgreSQL; QStash só dispara o dispatcher |
| 10 | Stripe só para a subscrição do treinador. Webhook: `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`, `customer.subscription.trial_will_end`, `invoice.paid`, `invoice.payment_failed` |
| 11 | Resend, Stripe, Cloudinary, QStash. RabbitMQ e MassTransit fora do MVP |
| 12 e 13 | Logs JSON na consola. Sentry só para erros. OpenTelemetry por OTLP para o Grafana Cloud. Região UE. Health checks. Render, Vercel, Neon. Destino decidido no Sprint 7A (ADR-001) |
| 17 | O que ficou para os Sprints 8, 10A, 10B, 10C e 10D |

Papéis, isolamento e o que o superuser não vê nos clientes estão nas regras `RN-001`, `RN-012` e `RN-018`.

## O que a arquitectura ainda não tem

Decisões de produto já escritas noutro sítio. Não são uma arquitectura nova.

| Data | Diferença ou decisão em falta | Onde está | Nota |
|---|---|---|---|
| 2026-10-15 | Reembolso da subscrição | `RNF-014` | Sem prioridade. Quando existir, é só no Stripe, sem ecrã. O painel desce quando o Stripe confirmar, só a parte devolvida. A allowlist do webhook ainda não inclui reembolso. |
| 2026-10-15 | Dinheiro do painel do superuser | `RN-018` | Checkout por pagar não entra. Só entram pagamentos confirmados. Não há tabela local desse número no esquema. |
| 2026-10-15 | Marca em todas as páginas da área do treinador | `RN` no fim de `business-rules.md`; ponto 14 do plano 6G | A 6E-5 limitava a marca ao portal. Passa a valer também na área do treinador, na 6GB-2. Os dados da marca já estão em `trainer_settings`. |
| 2026-10-25 | Destino da telemetria | `decisions/ADR-001-destino-telemetria.md` | A arquitectura (§12.2) dizia «confirmado no Sprint 0», e não estava. Ficou decidido no 7A. |
| 2026-10-15 | Copiar o link do convite | Ponto 13 do plano 6G; `FL-002` | Botão «Copiar link», sem o link sempre à vista. Entra na 6GB-2. O esquema guarda só o hash do token. |
