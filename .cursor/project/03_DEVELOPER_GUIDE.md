# PT Manager Backend — Developer Guide

*Como trabalhar no projeto C#/.NET 10*

---

## Setup Local

### Requisitos

```
.NET 10.0 SDK (https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
PostgreSQL 17 (local) OU Neon account
Docker (para Testcontainers)
Visual Studio 2026 / VS Code + C# Dev Kit
Git
```

### Instalação Passo-a-Passo

1. **Clone repositório**
   ```bash
   git clone https://github.com/seu-repo/ptmanager.git
   cd ptmanager/backend
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Setup Database**
   
   **Opção A: PostgreSQL Local**
   ```bash
   # macOS/Linux
   brew install postgresql@16
   brew services start postgresql@16
   
   # Criar database
   createdb ptmanager_dev
   ```
   
   **Opção B: Neon Cloud**
   ```
   Acesso: https://console.neon.tech
   Criar projecto, copiar connection string para appsettings.json
   ```

4. **Configure appsettings.json**
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=ptmanager_dev;Username=postgres;Password=postgres"
     },
     "Jwt": {
       "Secret": "dev_secret_key_min_32_chars_long",
       "AccessTokenMinutes": 15,
       "RefreshTokenDays": 30
     },
     "Stripe": {
       "SecretKey": "sk_test_..."
     },
     "Resend": {
       "ApiKey": "re_test_..."
     },
     "Upstash": {
       "RedisConnectionString": "localhost:6379",
       "QStashSigningKey": "dev_signing_key"
     }
   }
   ```
   Em dev local, o `RedisConnectionString` pode apontar para um Redis local (Docker) em vez do Upstash — a app não pode depender de Redis estar sempre disponível (`00_ARCHITECTURE.md §8.2`).

5. **Apply migrations**
   ```bash
   dotnet tool run dotnet-ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
   ```

6. **Run API**
   ```bash
   dotnet run --project src/Api/Api.csproj
   ```
   
   Acesso: http://localhost:5000
   Swagger UI: http://localhost:5000/swagger

   Em dev local não há QStash a chamar `/api/internal/jobs/dispatch` — chamar manualmente com `curl` ou um script simples para testar o dispatcher.

---

## Workflow Diário

### Build

```bash
# Restore reproduzível
dotnet restore PTManager.sln --locked-mode

# Build em modo Release
dotnet build PTManager.sln --configuration Release --no-restore
```

### Tests

```bash
# Rodar todos testes
dotnet test PTManager.sln --configuration Release --no-build

# Rodar testes específicos
dotnet test tests/Domain.UnitTests/Domain.UnitTests.csproj --configuration Release

# Com code coverage
dotnet test PTManager.sln --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults

# Watch mode (rerun testes ao salvar ficheiros)
dotnet watch --project tests/Application.UnitTests/Application.UnitTests.csproj test
```

### Development

```bash
# Hot reload — modificações refletem sem reiniciar
dotnet watch --project src/Api/Api.csproj run

# Apenas backend
dotnet run --project src/Api/Api.csproj --environment Development
```

### Database

```bash
# Criar migration nova
dotnet tool run dotnet-ef migrations add AddNewColumn --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output-dir Data/Migrations

# Aplicar migrations
dotnet tool run dotnet-ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj

# Reverter última migration
dotnet tool run dotnet-ef database update PreviousMigration --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj

# Script SQL (sem executar)
dotnet tool run dotnet-ef migrations script --idempotent --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj --output artifacts/migrations.sql

```

Confirmar sempre a base de dados alvo antes de aplicar ou reverter uma migration. Nunca editar uma migration já aplicada num ambiente partilhado. Uma correção usa uma migration nova. Ver `01_DATABASE_SCHEMA.md`.

### Análise Código

```bash
# Análise com Roslyn analyzers (já integrado)
dotnet build PTManager.sln --configuration Release --no-restore

# Verificar formatação sem alterar
dotnet format PTManager.sln --verify-no-changes --no-restore
```

---

## Estrutura Clean Architecture

**Directório** (ver `00_ARCHITECTURE.md §3`):
```
backend/
├── src/
│   ├── Api/               ← Controllers, Middlewares, HTTP endpoints
│   ├── Application/       ← Handlers, DTOs, Validators (por feature)
│   ├── Domain/            ← Entities, Value Objects, Interfaces
│   └── Infrastructure/    ← Repositórios, EF Core, External Services
└── tests/
    ├── Domain.UnitTests/
    ├── Application.UnitTests/
    ├── Infrastructure.IntegrationTests/
    ├── Api.FunctionalTests/
    └── ArchitectureTests/   ← valida as regras abaixo automaticamente
```

**Regras Importantes:**

| Layer | Pode importar | NÃO pode importar |
|-------|---------------|------------------|
| Api | Application | Infrastructure direto (só no composition root) |
| Application | Domain | Api, Infrastructure |
| Domain | Nada | Qualquer coisa |
| Infrastructure | Application, Domain | Api |

Estas regras são verificadas automaticamente pelo projeto `ArchitectureTests` — uma violação falha o CI, não depende de review manual.

---

## Common Tasks

### Adicionar Novo Endpoint

Sem AutoMapper, sem `IRepository<T>` genérico, sem MediatR — handler explícito por caso de uso (`00_ARCHITECTURE.md §2.3` e `§2.4`).

1. **Criar DTO (`Application/Features/Nutrition/Dtos/`)**
   ```csharp
   public record CreateMealPlanRequest(
       string Name,
       DateOnly StartsDate,
       DateOnly EndsDate,
       decimal ProteinTargetG);
   ```

2. **Criar Validator (`Application/Features/Nutrition/Validators/`)**
   ```csharp
   public class CreateMealPlanValidator : AbstractValidator<CreateMealPlanRequest>
   {
       public CreateMealPlanValidator()
       {
           RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
           RuleFor(x => x.StartsDate).LessThan(x => x.EndsDate);
       }
   }
   ```

3. **Criar Handler (`Application/Features/Nutrition/CreateMealPlanHandler.cs`)**
   ```csharp
   public class CreateMealPlanHandler
   {
       private readonly IMealPlanRepository _repository;
       private readonly ITenantContext _tenantContext;
       private readonly IValidator<CreateMealPlanRequest> _validator;

       public async Task<Result<MealPlanResponse>> HandleAsync(
           CreateMealPlanRequest request, CancellationToken ct)
       {
           var validation = await _validator.ValidateAsync(request, ct);
           if (!validation.IsValid)
               return Result<MealPlanResponse>.Validation(validation.Errors);

           var entity = new MealPlan(request.Name, _tenantContext.TrainerId, request.StartsDate, request.EndsDate);
           await _repository.AddAsync(entity, ct);

           return Result<MealPlanResponse>.Success(entity.ToResponse()); // mapping explícito, sem AutoMapper
       }
   }
   ```

4. **Criar Controller (`Api/Controllers/MealPlansController.cs`)** — fino, sem `DbContext` nem regras de negócio
   ```csharp
   [ApiController]
   [Route("api/v1/meal-plans")]
   [Authorize(Roles = "trainer")]
   public class MealPlansController(CreateMealPlanHandler handler) : ControllerBase
   {
       [HttpPost]
       public async Task<IActionResult> Create(
           [FromBody] CreateMealPlanRequest request, CancellationToken ct)
       {
           var result = await handler.HandleAsync(request, ct);
           return result.ToActionResult(this); // converte Result -> Problem Details ou 201
       }
   }
   ```

5. **Registar na DI (`Program.cs` / `AddApplication`)**
   ```csharp
   builder.Services.AddScoped<CreateMealPlanHandler>();
   builder.Services.AddScoped<IMealPlanRepository, MealPlanRepository>();
   builder.Services.AddValidatorsFromAssemblyContaining<CreateMealPlanValidator>();
   ```

### Adicionar Validator Complexo

```csharp
public class MealPlanValidator : AbstractValidator<CreateMealPlanRequest>
{
    public MealPlanValidator(IClientRepository clientRepository)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.StartsDate)
            .LessThan(x => x.EndsDate)
            .WithMessage("Start date must be before end date");

        // Async rule — valida contra a base de dados
        RuleFor(x => x.ClientId)
            .MustAsync(async (clientId, cancellation) =>
                await clientRepository.ExistsAsync(clientId, cancellation))
            .WithMessage("Client not found");
    }
}
```

### Adicionar Job Durável (dispatcher + QStash, sem RabbitMQ)

Ver `00_ARCHITECTURE.md §9` e `01_DATABASE_SCHEMA.md` tabela `durable_jobs`. Não existe broker — o job é uma linha em Postgres, reclamada pelo dispatcher quando o QStash acorda a API.

1. **Enfileirar o job no handler que originou a necessidade**
   ```csharp
   public class CreateClientHandler
   {
       private readonly IClientRepository _clients;
       private readonly IDurableJobRepository _jobs;

       public async Task<Result<ClientResponse>> HandleAsync(CreateClientRequest request, CancellationToken ct)
       {
           var client = new Client(request.Name, _tenantContext.TrainerId);
           await _clients.AddAsync(client, ct);

           // Mesma unidade de trabalho — o job só existe se o cliente for criado
           await _jobs.EnqueueAsync(new DurableJob(
               jobType: "send_welcome_email",
               trainerId: client.OwnerTrainerId,
               payload: new { client.Id, client.Name },
               idempotencyKey: $"welcome-email:{client.Id}"), ct);

           return Result<ClientResponse>.Success(client.ToResponse());
       }
   }
   ```

2. **Implementar o handler do job (`Application/Features/Jobs/SendWelcomeEmailJobHandler.cs`)**
   ```csharp
   public class SendWelcomeEmailJobHandler : IJobHandler
   {
       public string JobType => "send_welcome_email";
       private readonly IEmailSender _emailSender;

       public async Task HandleAsync(DurableJob job, CancellationToken ct)
       {
           var payload = job.DeserializePayload<SendWelcomeEmailPayload>();
           _logger.LogInformation("[JOBS] Sending welcome email to client {ClientId}", payload.ClientId);
           await _emailSender.SendWelcomeAsync(payload.Email, payload.ClientName, ct);
           // idempotente: reenviar o mesmo email duas vezes não deve causar efeito duplicado visível
       }
   }
   ```

3. **O dispatcher (`Infrastructure/Jobs/JobDispatcher.cs`) é único e genérico** — reclama jobs vencidos, resolve o `IJobHandler` pelo `JobType`, cria `ITenantContext` a partir do `TrainerId` persistido, chama `HandleAsync`. Não se escreve um dispatcher por feature.

### Structured Logging

```csharp
private readonly ILogger<CreateMealPlanHandler> _logger;

// CORRETO — queryable, structured
_logger.LogInformation(
    "[NUTRITION] Created meal plan {PlanId} for client {ClientId} with {MacroCount} macros",
    planId, clientId, macroCount);

// ERRADO — string interpolation (allocates even if filtered)
_logger.LogInformation($"[NUTRITION] Created {planId} for {clientId}");

// Com dados estruturados — ILogger.BeginScope, sem Serilog (00_ARCHITECTURE.md §12.1)
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["TrainerId"] = trainerId,
    ["CorrelationId"] = correlationId
}))
{
    _logger.LogInformation("[NUTRITION] Processing meal plan");
}
```

---

## Git Workflow

### Branch Naming

```
feature/auth-jwt-refresh
bugfix/meal-plan-calculation
test/add-repository-tests
chore/update-dependencies
```

### Commit Messages

```
feat: add JWT token refresh endpoint
fix: correct macro calculation in MacroCalculatorHandler
test: add validation tests for meal plan creation
chore: upgrade EF Core to 10.0.1
docs: add developer guide for durable jobs
```

### Pull Request Checklist

- ✓ Branch atualizado com `main`
- ✓ Testes locais passam (`dotnet test`)
- ✓ Code coverage ≥ 80%
- ✓ Sem warnings em build (`dotnet build`)
- ✓ Comentários em código complexo
- ✓ Sem credenciais ou secrets
- ✓ Commit messages descritivos

---

## Debugging

### Visual Studio

```
F5 — Start debugging
F10 — Step over
F11 — Step into
Shift+F11 — Step out
Ctrl+Alt+W — Watch window
```

### VS Code

Instalar extensão: "C# Dev Kit"

Launch config (`.vscode/launch.json`):
```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": ".NET Core Launch",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/src/Api/bin/Debug/net10.0/Api.dll",
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false
        }
    ]
}
```

### Console Debugging

```csharp
// Log ao stderr (visível em console)
System.Diagnostics.Debug.WriteLine($"Debug: {variable}");

// Breakpoint condicional
if (variable == problematicValue)
{
    System.Diagnostics.Debugger.Break();
}
```

---

## Performance Tips

### EF Core

```csharp
// ✓ BOAS PRÁTICAS

// 1. Explicit loading — evita N+1
var plans = await _context.MealPlans
    .Include(p => p.Meals)
    .ThenInclude(m => m.Items)
    .ToListAsync();

// 2. Projection — selects apenas campos necessários
var dtos = await _context.MealPlans
    .Select(p => new MealPlanReadDto
    {
        Id = p.Id,
        Name = p.Name
        // Sem trazer Meals inteiras
    })
    .ToListAsync();

// 3. Async sempre
await _context.SaveChangesAsync();

// 4. Indexes em queries críticas
modelBuilder.Entity<Client>()
    .HasIndex(c => new { c.OwnerTrainerId, c.IsDeleted });
```

### Caching

```csharp
// ✓ HybridCache (L1 in-memory + L2 distributed)
var plans = await _cache.GetOrCreateAsync(
    $"plans:{trainerId}",
    async factory =>
    {
        return await _repository.GetActivePlansAsync(trainerId);
    },
    new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromHours(1)
    }
);
```

---

## Troubleshooting

### `dotnet restore` falha

```bash
# Obter diagnóstico sem alterar caches globais
dotnet restore PTManager.sln --locked-mode --verbosity diagnostic

# Atualizar lock files apenas depois de uma alteração intencional de packages
dotnet restore PTManager.sln
```

### Migrations não rodam

```bash
# Restaurar ferramentas e consultar o histórico sem alterar a base de dados
dotnet tool restore
dotnet tool run dotnet-ef migrations list --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj

```

Não apagar migrations para resolver um erro. Confirmar primeiro o `DbContext`, o startup project, a connection string do ambiente e o histórico aplicado. Se uma migration já foi partilhada ou aplicada, a correção é sempre uma migration nova.

### Testes timeout

```bash
# Aumentar timeout (appsettings.json)
"ConnectionStrings": {
    "DefaultConnection": "...;Command Timeout=30;"
}

# Ou rodar testes com timeout maior
dotnet test --logger:"console;verbosity=detailed" /p:TestTimeout=60000
```

### "Column 'X' not found" em queries

**Causa:** EF Core não gerou coluna ou migration não aplicada.

```bash
# Check migrations não aplicadas
dotnet tool run dotnet-ef migrations list --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj

# Aplicar todas
dotnet tool run dotnet-ef database update --project src/Infrastructure/Infrastructure.csproj --startup-project src/Api/Api.csproj
```

---

## Recursos Úteis

- **EF Core Docs:** https://learn.microsoft.com/en-us/ef/core/
- **ASP.NET Core Docs:** https://learn.microsoft.com/en-us/aspnet/core/
- **FluentValidation:** https://docs.fluentvalidation.net/
- **Upstash QStash:** https://upstash.com/docs/qstash
- **Upstash Redis:** https://upstash.com/docs/redis
- **OpenTelemetry .NET:** https://opentelemetry.io/docs/languages/net/

---

*Fim do Developer Guide*
