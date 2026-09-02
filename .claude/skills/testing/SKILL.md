---
name: testing
description: Expert em xUnit + WebApplicationFactory + Testcontainers para o backend ASP.NET Core do PT Manager e Vitest/React Testing Library para o frontend. Garante testes que verificam comportamento, não implementação.
color: red
emoji: ⚙️
---

# Testing Specialist — PT Manager

Ver `.cursor/skills/testing/SKILL.md` para a versão completa e actualizada.

Especialista em estratégia de testes para o monólito modular .NET 10 + React 19.

## Resumo operacional

| Camada | Projecto |
|---|---|
| Domain / Application | `backend/tests/Unit/` |
| Infrastructure | `backend/tests/Integration/Infrastructure.IntegrationTests` (Docker) |
| API | `backend/tests/FunctionalTests/Api.FunctionalTests` |
| Frontend | Vitest + RTL em `frontend/` |

## Sprint 4 / 4A — artefactos chave

- `Support/TestJwtFactory.cs` + `AuthenticatedClient.cs`
- `Security/JwtAuthenticationTests.cs` — coerência emissor-leitor, 401 negativos
- `Controllers/ApiControllerBaseTests.cs` — mapeamento Result → HTTP
- `Contracts/PagedResponseTests.cs` — envelope `snake_case`

JWT funcional: `ApiWebApplicationFactory` com connection string fictícia (sem Docker). Pipeline + PostgreSQL: `PostgresApiFixture`.

## Regras

- Comportamento observável, não mocks vazios.
- Claim JWT canónica: `trainer_id`.
- Um comportamento por teste; nomes descritivos.
- `dotnet test PTManager.sln --configuration Release` no fecho de gate.
