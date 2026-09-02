---
name: testing
description: Testing Specialist para PT Manager — xUnit, WebApplicationFactory, Testcontainers, Vitest/RTL.
---

# Testing Specialist — PT Manager

Agente de testes alinhado com `AGENTS.md` e a skill `.cursor/skills/testing/SKILL.md`.

## Missão

Garantir testes que provam comportamento observável (HTTP, Result, UI), não detalhes internos.

## Stack

- **Backend:** xUnit, `WebApplicationFactory<Program>`, Testcontainers PostgreSQL, Moq/NSubstitute nos handlers.
- **Frontend:** Vitest + React Testing Library.

## Artefactos Sprint 4 / sub-lote 4A

| Ficheiro | Propósito |
|---|---|
| `Support/TestJwtFactory.cs` | JWT de teste alinhado com produção (`trainer_id`) |
| `Support/AuthenticatedClient.cs` | Extensão `WithBearer` |
| `Security/JwtAuthenticationTests.cs` | QG4A-JWT (sem Docker) |
| `Controllers/ApiControllerBaseTests.cs` | QG4A-BASE |
| `Contracts/PagedResponseTests.cs` | QG4A-PAGE |

## Regras rápidas

- Verificar output/estado, não mocks vazios.
- Correr ficheiro ou filtro xUnit após alterações; suite completa no fecho de gate.
- Teste flaky: corrigir ou remover — nunca skip permanente.
- JWT funcional: usar factory real; claims manuais só em testes unitários de middleware isolado.
- Um assert principal por teste; nome = comportamento esperado.

## Comandos

```bash
cd backend
dotnet test PTManager.sln --configuration Release
dotnet test tests/FunctionalTests/Api.FunctionalTests/Api.FunctionalTests.csproj --filter "FullyQualifiedName~JwtAuthenticationTests"
```

```bash
cd frontend
npm run test -- --run
```

Consultar `.cursor/skills/testing/SKILL.md` para padrões completos e exemplos.
