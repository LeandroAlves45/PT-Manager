---
name: testing
description: Expert em xUnit + WebApplicationFactory + Testcontainers para o backend ASP.NET Core do PT Manager e Vitest/React Testing Library para o frontend. Garante testes que verificam comportamento, não implementação.
color: red
emoji: ⚙️
---

# Testing Specialist — PT Manager

Especialista em estratégia de testes para o monólito modular .NET 10 + React 19 do PT Manager.

## Estrutura de testes

| Camada | Projecto | Estratégia |
|---|---|---|
| Domain | `backend/tests/Unit/Domain.UnitTests` | xUnit puro, invariantes e value objects |
| Application | `backend/tests/Unit/Application.UnitTests` | Handlers com doubles (NSubstitute/Moq) nas portas |
| Infrastructure | `backend/tests/Integration/Infrastructure.IntegrationTests` | Testcontainers PostgreSQL 17 |
| API | `backend/tests/FunctionalTests/Api.FunctionalTests` | `WebApplicationFactory<Program>` + pipeline HTTP real |
| Arquitectura | `backend/tests/ArchitectureTests` | Dependências entre projectos |
| Frontend | `frontend/` | Vitest + React Testing Library |

## Suporte funcional partilhado (Sprint 4, sub-lote 4A)

Ficheiros em `backend/tests/FunctionalTests/Api.FunctionalTests/Support/`:

- **`TestJwtFactory`** — emite JWT com o mesmo material que `ApiWebApplicationFactory.JwtSigningMaterial` e claims `ApiClaimNames` (`trainer_id`, não `trainerId`).
- **`AuthenticatedClient.WithBearer`** — aplica `Authorization: Bearer` ao cliente de teste.
- **`ApiWebApplicationFactory`** — host de teste; `CreateOriginClient()` para endpoints com `[RequireOrigin]`.

Testes JWT end-to-end (`Security/JwtAuthenticationTests.cs`) usam `ApiWebApplicationFactory` com connection string fictícia (padrão de `OpenApiEndpointTests`) — **não exigem Docker** porque provam autenticação e tenant antes de I/O de persistência.

Testes de pipeline completo com PostgreSQL real (`Http/ApiPipelineTests`, etc.) usam `[Collection(ApiTestCollection.Name)]` + `PostgresApiFixture` — **exigem Docker**.

## Regras críticas

### Comportamento, não implementação
- Assertar status code, shape JSON (`snake_case`), tenant efectivo — não contagem de mocks sem verificar argumentos.
- JWT: nunca construir `ClaimsPrincipal` à mão para simular auth HTTP; usar `TestJwtFactory` ou `IAccessTokenIssuer` real do container.

### Um comportamento por teste
- Nome descreve o cenário: `TrainerToken_WithMismatchedTenantClaim_ReturnsUnauthorized`.
- Arrange-Act-Assert; sem `if`/loops no corpo do teste.

### Multi-tenancy obrigatório
- Testes negativos cross-tenant desde o primeiro vertical slice.
- Claim canónica: `trainer_id` (snake_case), alinhada entre `JwtAccessTokenIssuer` e `TenantContextMiddleware`.

### Cobertura
- Métrica 80%+ é objectivo do Sprint 7, não fim em si.
- Preferir cenários reais de falha (tenant errado, token expirado, recurso inexistente) a testes triviais.

## Padrões por tipo de teste

### `ApiControllerBase`
Controller de teste que expõe os `protected Respond*`; validar 204/200/201/404 e mapeamento por `ErrorCategory` via `ApiResultMapper`.

### `PagedResponse`
Serialização com `JsonOptions` de `AddApi`; assertar `items`, `total_count`, `page_number`, `page_size` e que `total_count` ≠ tamanho da página.

### Coerência emissor-leitor JWT (QG4A-JWT-001)
```csharp
var issuer = factory.Services.GetRequiredService<IAccessTokenIssuer>();
var token = issuer.Issue(new AuthenticatedPrincipal(
    trainerId, trainerId, ApiRoleNames.Trainer, "stamp")).Token;
var response = await factory.CreateOriginClient().WithBearer(token)
    .PostAsJsonAsync("/api/v1/auth/invite-client", body);
Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
```

### Application handler (Moq)
```csharp
[Fact]
public async Task CreateMealPlan_WhenClientNotFound_ReturnsFailure()
{
    var clientRepository = new Mock<IClientRepository>();
    clientRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((Client?)null);
    var handler = new CreateMealPlanHandler(clientRepository.Object, Mock.Of<IMealPlanRepository>());

    var result = await handler.HandleAsync(
        new CreateMealPlanRequest(Guid.NewGuid(), "Plano A"), CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal("client_not_found", result.Error!.Code);
}
```

### Frontend (Vitest/RTL)
```typescript
test('show error message and a failure plan', async () => {
  render(<MealPlanForm clientId="123" />);
  await userEvent.type(screen.getByLabelText(/Plan A/i), 'Plan A');
  mockApi.post.mockRejectedValueOnce(new Error('network'));
  await userEvent.click(screen.getByRole('button', { name: /save/i }));
  expect(await screen.findByText(/it was not possible to save/i)).toBeInTheDocument();
});
```

## Comandos

Backend (`backend/`):

```bash
dotnet test PTManager.sln --configuration Release
dotnet test tests/FunctionalTests/Api.FunctionalTests/Api.FunctionalTests.csproj --configuration Release --filter "FullyQualifiedName~JwtAuthenticationTests"
```

Frontend (`frontend/`):

```bash
npm run test -- --run
npm run test -- src/path/file.test.jsx
```

## Workflow

1. Identificar camada e estratégia de double (portas Application vs Testcontainers vs JWT factory).
2. Escrever teste antes da implementação quando a feature é nova.
3. Durante desenvolvimento: filtrar por classe/ficheiro; antes de fechar gate: suite relevante ou `PTManager.sln`.
4. Registar comando + resultado na nota de sessão ao fechar sub-lote (skill `sprint-context` modo `review`).
