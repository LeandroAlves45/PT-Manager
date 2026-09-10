# Estado ativo: Sprint 5B fechada; falta apenas configurar secrets Stripe

Atualizado: 2026-09-10

## Estado em uma linha

O Sprint 4 e a Fase 5A estão fechados. A Fase 5B está implementada no backend real,
revista contra os blueprints, corrigida e verde: 2171 testes, build Release 0/0 e
`dotnet format` exit 0. Cinco bugs de produção foram corrigidos, três deles críticos.
A migration `20260910125857_AddStripeBillingOperations` foi aplicada à base local
`ptmanager_dev` (Docker `ptmanager-postgres-dev`, porta 5437) e verificada no schema
efetivo, com a API a arrancar limpa contra ela. Ver
`docs/backend-files/sprint_5/sprint_5B/17_fecho_implementacao_achados_correcoes_testes.md`.

## Confirmações do utilizador

1. A migration `20260908141012_AddQStashDispatchReceipts` foi aplicada localmente.
2. Os user-secrets QStash current key e next key foram configurados.
3. Os valores não foram lidos nem guardados nesta documentação.
4. `QStash:DestinationUrl` ainda não existe por falta de host público.
5. `QStash:Enabled` permanece `false`.

## Sprint 5B

1. Stripe.net 52.4.1 e API `2026-08-26.dahlia`.
2. URLs apenas em configuração backend e campos do caller rejeitados.
3. Checkout com intenção durável, lease e uma operação ativa por trainer.
4. A mesma Idempotency-Key retoma Pending/Failed/Expired; não cria outra linha.
5. FREE 5, STARTER 25 e PRO ilimitado.
6. Webhook raw body, correlation estável por `event.id`, rotação de secret, allowlist, reconciliação e outbox.
7. `billing_notification` limitado a payment_failed e trial_will_end.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-09-sprint5b-blueprints-retry.md`.
3. `docs/backend-files/sprint_5/sprint_5B/00_desenho_aprovado_indice_dependencias_gates.md`.
4. `docs/backend-files/sprint_5/sprint_5B/16_rastreabilidade_revisao_quality_gates.md`.
5. `backlogs/QualityGates.md`, secção Sprint 5 Fase 5B.

## Gates ainda abertos

`QG5B-STRIPE-001` e `QG5B-DEPLOY-001`: Stripe sandbox/CLI, host público, Price IDs,
webhook secret, IP allowlisting e ativação. Dependem de recursos externos e não são
fecháveis por código. Os gates de build, testes, migration e OpenAPI estão fechados.

## Passo imediato do utilizador

Configurar os secrets `Stripe:*` (nenhum existe ainda em user-secrets). Obrigatorios
quando `Stripe:Enabled=true`: `SecretKey`, `CurrentWebhookSecret`, `StarterPriceId`,
`ProPriceId` (tem de diferir do STARTER) e as quatro URLs `CheckoutSuccessUrl`,
`CheckoutCancelUrl`, `PortalReturnUrl` e `BillingManagementUrl` — todas HTTPS absolutas,
sem user-info nem fragmento, e todas no mesmo host canonico. `NextWebhookSecret` e
opcional e existe para rotacao. Manter `Stripe:Enabled=false` ate tudo estar configurado:
`ValidateOnStart` derruba o arranque da API se faltar algum.

## Evidência da implementação 5B

426 Domain, 524 Application, 53 Architecture, 588 Infrastructure (PostgreSQL real) e
580 API, mais um teste manual de OpenAPI ignorado por desenho. Migration idêntica à
referência do documento 14, com ciclo migrate, rollback bloqueado por preflight,
downgrade, rollback e migrate validado em PostgreSQL 17.10 descartável. Cobertura dos
bugs confirmada por mutação controlada. Ver os documentos 16 e 17 do pack e
`.claude/memory/Sessions/2026-09-10-sprint5b-fecho-implementacao.md`.
