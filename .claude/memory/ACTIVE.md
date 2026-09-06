# Fase ativa: Sprint 4, Fase 5 — FECHADA

Atualizado: 2026-09-06
Modo actual: backend fechado; migration local aplicada; frontend Google diferido

## Estado em uma linha

Google Sign-In implementado no backend real (docs 01 a 06), com testes reais (docs 07, 08
e 09) e migration `20260906154210_AddExternalIdentities` aplicada à base local Docker
(PostgreSQL 17, `ptmanager-postgres-dev`). A suite integral está verde: 1896 aprovados,
1 ignorado, 0 falhas.

## Decisões fechadas (inalteradas)

1. Quatro operações em `/api/v1/auth/google`: challenge, sign-in, link/challenge e link.
2. Identidade por `provider + subject`; nunca por email.
3. Linking explícito com JWT PT Manager, password atual, challenge próprio e email igual.
4. Trainer com Gmail ou Workspace autoritativo recebe sessão imediata.
5. Trainer com outro domínio recebe 202 e confirmação PT Manager, sem sessão.
6. Client novo exige convite válido e email coincidente.
7. Nonce persiste apenas como hash e é consumido atomicamente sob lock.
8. `Google.Apis.Auth` fica exclusivamente em Infrastructure.

## Evidência de fecho

1. `dotnet build PTManager.sln -c Release`: 0 avisos, 0 erros.
2. `dotnet test PTManager.sln -c Release`: 1896 aprovados, 1 ignorado, 0 falhas.
   O ignorado é `RegenerateSnapshot`, que existe para corrida manual.
3. `dotnet format --verify-no-changes`: aprovado em toda a solução, sem exceções.
4. `has-pending-model-changes`: sem alterações pendentes.
5. PostgreSQL 17 descartável: migrate, rollback e migrate aprovados; rollback com conta
   passwordless recusado com a mensagem do preflight, schema intacto.
6. Snapshot de contrato: 131 para 135 operações, quatro rotas Google com atores corretos.
7. Base local Docker: `__EFMigrationsHistory` com 7 migrations aplicadas por ordem;
   tabelas `external_identities` e `external_authentication_challenges` presentes;
   `users.password_hash` nullable.
8. `Google:ClientId` configurado em user secrets; API arranca em Development.

## Bug de produção corrigido nesta sessão

`ExternalAuthenticationStore` estabelecia o tenant depois de `SaveChangesAsync`. Como
`TrainerSettings`/`TrainerSubscription` (política A') e `Client` (política A) exigem tenant
efetivo no interceptor, todo o onboarding Google falhava com 500. O `Establish` passou a
preceder a escrita, como já fazia o `AuthenticationRegistrationStore` local. Encontrado
pelos testes funcionais da fase.

## Por fazer

1. `QG5-FRONTEND-001` continua aberto por decisão: esta fase não alterou React.
   O frontend mantém contrato histórico e persistência de access token em `localStorage`.

## Ler nesta ordem

1. Este ficheiro.
2. `docs/backend-files/sprint_4/fase_5/13_fase_5_implementacao_concluida.md`.
3. `backlogs/QualityGates.md`, secção Sprint 4 Fase 5.
4. `.claude/memory/Sessions/2026-09-06-sprint4-fase5-migration-local-aplicada.md`.
5. `.claude/project/02_SPRINTS_ROADMAP.md` para o Sprint 5.

## Blockers

Nenhum no backend. A dívida restante do Sprint 4 é exclusivamente frontend (`QG5-FRONTEND-001`).
