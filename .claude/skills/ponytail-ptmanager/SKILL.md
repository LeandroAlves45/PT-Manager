---
name: ponytail-ptmanager
description: |
  YAGNI-driven code generation for C# .NET backend. Use ALWAYS when implementing features, refactoring, or writing handlers in PT Manager's Clean Architecture backend (Domain, Application, Infrastructure, Api layers). Triggers on: implementing handlers, creating entities, writing infrastructure code, refactoring features, fixing bugs, creating tests. This skill stops you from over-engineering. Reach for the smallest solution that works, reject premature abstractions, and avoid adding features you don't need right now.
---

# Ponytail: YAGNI Coding Skill for PT Manager C# Backend

You are implementing features in a C# .NET 10 backend with Clean Architecture organized by feature. Your job is to **stop at the first thing that works** — no premature abstraction, no generic repositories, no "just in case" interfaces.

## Core Principle: YAGNI (You Aren't Gonna Need It)

Before writing ANY code:

1. **Read the requirement once.** What's the actual need? Not what *might* be needed in 6 months.
2. **Find the simplest solution.** Can you use EF Core directly? A concrete class instead of an interface? A single handler instead of a pattern?
3. **Stop when it works.** If it passes the test, it's done. Don't refactor, don't add layers, don't generalize.

This is not laziness — it's *clarity*. Simpler code is easier to test, understand, and change.

## PT Manager Architecture Context

- **Clean Architecture:** Api → Application → Domain ← Infrastructure
- **Organization:** By feature (feature folders), not horizontal layers
- **Handlers:** One handler per operation (CreateClient, UpdateClientName, ArchiveClient are separate)
- **No Generics:** No `IRepository<T>`, no MediatR, no AutoMapper — mapping and dispatch are explicit
- **Multi-tenancy:** `owner_trainer_id` on entities, applied via EF Core Global Query Filters linked to `ITenantContext`
- **Async patterns:** All Application operations are async; use `ConfigureAwait(false)` sparingly (only in shared libraries)

## Code Patterns: The Simplest Path

### Handler Pattern (simplest form)

```csharp
// Application/Features/Clients/Commands/CreateClientHandler.cs
namespace Application.Features.Clients.Commands;

public class CreateClientHandler
{
    private readonly IClientsRepository _repository;
    private readonly ITenantContext _tenantContext;

    public CreateClientHandler(IClientsRepository repository, ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<ClientDto> Handle(CreateClientRequest request)
    {
        // Validate the request — explicit, not a library
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Name is required.");

        // Create the entity
        var client = new Client(
            id: Guid.NewGuid(),
            ownerTrainerId: _tenantContext.TrainerId,
            name: request.Name,
            email: request.Email
        );

        // Persist
        await _repository.AddAsync(client);
        await _repository.SaveChangesAsync();

        // Return
        return new ClientDto(client.Id, client.Name, client.Email);
    }
}
```

**Why this is perfect:**
- Single responsibility: Create a client, save it, return the DTO
- No MediatR dispatcher — it's called directly from the controller
- No AutoMapper — the DTO mapping is 3 lines
- Testable: inject the repository and context, mock them
- **Done.** Don't add a base class, don't add an interface wrapper, don't plan for "future query handlers"

### Entity Pattern (minimal)

```csharp
// Domain/Features/Clients/Client.cs
namespace Domain.Features.Clients;

public class Client
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; } // Multi-tenancy
    public string Name { get; private set; }
    public string Email { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    // Constructor
    public Client(Guid id, Guid ownerTrainerId, string name, string email)
    {
        Id = id;
        OwnerTrainerId = ownerTrainerId;
        Name = name;
        Email = email;
        CreatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    // Only add methods that the domain needs, right now
    public void UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Name cannot be empty.");
        Name = newName;
    }
}
```

**Why this works:**
- No fancy patterns, no value objects unless the domain demands it
- Constructor sets the basics; methods express behavior
- **Stop here.** Don't add `IEntity<T>`, don't add audit trails unless required, don't create a base class

### Repository: Concrete, Not Generic

```csharp
// Infrastructure/Persistence/Repositories/ClientsRepository.cs
namespace Infrastructure.Persistence.Repositories;

public class ClientsRepository : IClientsRepository
{
    private readonly ApplicationDbContext _context;

    public ClientsRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(Guid id)
    {
        return await _context.Clients.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        return await _context.Clients.ToListAsync();
    }

    public async Task AddAsync(Client client)
    {
        await _context.Clients.AddAsync(client);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
```

**Why:**
- One interface per entity, one implementation
- Concrete methods for what you actually need
- No generics, no base repository, no "I might need this later"
- **Stop when it works.**

### Global Query Filters (Multi-tenancy)

```csharp
// Infrastructure/Persistence/ApplicationDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Global filter: all Client queries automatically filter by owner_trainer_id
    modelBuilder.Entity<Client>()
        .HasQueryFilter(c => c.OwnerTrainerId == _tenantContext.TrainerId);

    // Same for other tenant-owned entities
    modelBuilder.Entity<Session>()
        .HasQueryFilter(s => s.OwnerTrainerId == _tenantContext.TrainerId);
}
```

**Why:**
- Automatic filtering everywhere — no SQL injection, no forgotten WHERE clauses
- Injected `ITenantContext` guarantees you're always in the right tenant
- Simple, clear, done

## Anti-Patterns: Stop Before You Reach These

### ❌ Don't do this

```csharp
// WRONG: Generic repository
public interface IRepository<T> where T : class
{
    Task<T> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task DeleteAsync(Guid id);
}

// WRONG: Base entity class for every entity
public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
}

// WRONG: AutoMapper for trivial mappings
public class ClientProfile : Profile
{
    public ClientProfile()
    {
        CreateMap<Client, ClientDto>();
    }
}

// WRONG: MediatR for simple dispatch
public class CreateClientCommand : IRequest<ClientDto> { }
public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, ClientDto> { }

// WRONG: Value Objects for simple strings
public class Email : ValueObject
{
    public string Value { get; }
    // 50 lines of equality and hashing code for a string
}
```

**Why not?** These add layers, complexity, and cognitive overhead without solving today's problems. If the requirement changes in 6 months, you refactor then.

## Testing: Explicit Mocks, No Fancy Frameworks

```csharp
// Tests/Application.UnitTests/Features/Clients/CreateClientHandlerTests.cs
public class CreateClientHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_CreatesAndReturnsClient()
    {
        // Arrange
        var mockRepository = new Mock<IClientsRepository>();
        var mockTenantContext = new Mock<ITenantContext>();
        mockTenantContext.Setup(x => x.TrainerId).Returns(Guid.NewGuid());

        var handler = new CreateClientHandler(mockRepository.Object, mockTenantContext.Object);
        var request = new CreateClientRequest { Name = "John Doe", Email = "john@example.com" };

        // Act
        var result = await handler.Handle(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Doe", result.Name);
        mockRepository.Verify(x => x.AddAsync(It.IsAny<Client>()), Times.Once);
        mockRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var handler = new CreateClientHandler(new Mock<IClientsRepository>().Object, new Mock<ITenantContext>().Object);
        var request = new CreateClientRequest { Name = "", Email = "john@example.com" };

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(request));
    }
}
```

**Why:**
- Mocks are explicit — you see what's being tested
- One assertion per test (or one behavior per test)
- No fancy test builders or fixtures — just `new Handler(mock, mock)`
- **Done.** When the test passes, the feature is done

## Refactoring Guidance: When to Introduce Abstraction

Only when:
1. **The code repeats 3+ times** across different handlers
2. **The requirement explicitly asks for it** (e.g., "support multiple payment gateways")
3. **The test suite reveals a genuine pain point** (e.g., mocking gets so complex that an interface simplifies it)

Until then: **One handler, one repository, one concrete class.** Simplicity wins.

## Checklist: Before Calling Code "Done"

- ✓ Handler solves the exact requirement (not "what might be needed")
- ✓ Tests pass (unit + integration if touch DB)
- ✓ Multi-tenancy filter applied (if entity owns data)
- ✓ Validations are explicit (not buried in a library)
- ✓ DTOs map manually (no AutoMapper)
- ✓ No generic base classes, no interfaces for "future use"
- ✓ Commits are small and focused
- ✓ Code is readable to a developer unfamiliar with the project

**Stop here. Ship it.**
