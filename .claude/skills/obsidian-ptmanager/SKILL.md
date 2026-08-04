---
name: obsidian-ptmanager
description: |
  Persistent memory system for PT Manager using Obsidian vault in .claude/memory/. Use at the START of each session to load project context and at the END to capture lessons learned. Triggers on: new session, "load memory", "save session notes", project reviews, retrospectives, or when you need continuity across chats. Stores architectural decisions, gotchas, patterns, and session summaries in durable .md files with git history.
---

# Obsidian: Persistent Memory for PT Manager

This skill manages the `.claude/memory/` vault in your PT Manager project folder. It provides **durable, versionable, searchable context** that persists across chat sessions.

The entire knowledge base is **diffable, branchable, and has full git history** — if something changes, you can see exactly what, when, and why.

## Memory Structure: How It Works

Your `.claude/memory/` directory is organized like this:

```
.claude/memory/
├── MEMORY.md                    # Index: starting point for every session
├── Sessions/                    # Session summaries
│   ├── 2026-07-25-backend-sprint.md
│   ├── 2026-07-24-frontend-fixes.md
│   └── ...
├── Gotchas/                     # Common pitfalls (named gotcha_*.md)
│   ├── gotcha_multi_tenancy.md
│   ├── gotcha_migrations.md
│   └── ...
├── Architecture/                # Architecture decisions
│   ├── clean_architecture.md
│   ├── handlers_pattern.md
│   └── ...
├── Patterns/                    # Reusable code patterns
│   ├── global_query_filters.md
│   ├── entity_construction.md
│   └── ...
└── Corrections/                 # Patterns from mistakes corrected
    ├── correction_over_abstraction.md
    └── ...
```

## At the START of Every Session: Load Memory

**Step 1: Read MEMORY.md**

This is your entry point. It should contain:
- Quick summary of the project (PT Manager: SaaS, C# backend rewrite, React frontend)
- Current focus (Sprint number, feature being worked on)
- Key architectural decisions (Clean Architecture, no MediatR, multi-tenancy via Global Query Filters)
- Link to most recent session notes

Example MEMORY.md:

```markdown
# PT Manager — Working Memory

**Project:** SaaS for personal trainers (clients, sessions, plans, nutrition, billing)
**Language:** C# .NET 10 (backend rewrite from Python), React 19 (frontend)
**Architecture:** Clean Architecture, modular monolith, organized by feature
**Database:** PostgreSQL 17 (Neon), EF Core 10, multi-tenancy via `owner_trainer_id` + Global Query Filters

## Current Status
- **Sprint:** Sprint 1 (Domain Layer)
- **Focus:** Building entites, value objects, domain logic
- **Not doing:** RabbitMQ, MediatR, AutoMapper, `IRepository<T>` generic
- **Key constraint:** No migrations written by hand; all via `dotnet ef migrations add`

## Last Session
- Date: 2026-07-25
- Work: Implemented Client entity, SessionRepository pattern
- Link: `Sessions/2026-07-25-backend-sprint.md`

## Quick Links
- Architecture: `.claude/project/00_ARCHITECTURE.md` (source of truth)
- Database: `.claude/project/01_DATABASE_SCHEMA.md`
- Sprint plan: `.claude/project/02_SPRINTS_ROADMAP.md`
- Key gotchas: `Gotchas/gotcha_multi_tenancy.md`, `Gotchas/gotcha_migrations.md`
```

**Step 2: Read Gotchas**

Skim relevant `gotcha_*.md` files if you're working in a known problem area (e.g., multi-tenancy, migrations, testing).

**Step 3: Skim Recent Sessions**

Read the last 1-2 session summaries to understand what happened before and what's blocked.

## At the END of Every Session: Save Memory

**Step 1: Create Session File**

Filename: `.claude/memory/Sessions/YYYY-MM-DD-topic.md` (e.g., `2026-07-25-backend-sprint.md`)

Content:

```markdown
# Session: Backend Sprint — 2026-07-25

**Duration:** ~2 hours
**Focus:** Domain layer (Client entity, SessionRepository)
**Status:** In progress / Completed / Blocked

## What I Did
- Implemented `Client` entity with proper multi-tenancy isolation
- Created `IClientsRepository` and `ClientsRepository` (concrete, not generic)
- Wrote unit tests for `CreateClientHandler`
- Fixed `Global Query Filter` to include `Session` entity

## Key Decisions Made
- Decided against `Value Objects` for Email/Name (too early to abstract)
- Confirmed handler pattern: one handler per operation (no MediatR)
- Chose `Guid` generation in C# (`Guid.NewGuid()`), not Postgres (`gen_random_uuid()`)

## Blockers / Issues
- None this session

## Learnings / Patterns Discovered
- Multi-tenancy filter must be applied at DbContext level, not repository level
  → Pattern documented in `Patterns/global_query_filters.md`
- Test mocks should inject `ITenantContext` explicitly
  → Example: `mockTenantContext.Setup(x => x.TrainerId).Returns(Guid.NewGuid())`

## Next Steps
1. Implement `Trainer` entity and `TrainerRepository`
2. Build integration tests with real DbContext
3. Test soft delete behavior with `IsDeleted` flag

## Commits
- `feat(domain): implement Client entity with multi-tenancy`
- `feat(infrastructure): create ClientsRepository with Global Query Filters`
- `test: add CreateClientHandler unit tests`
```

**Step 2: Update MEMORY.md**

Update the "Last Session" section and "Key constraints" if anything changed.

**Step 3: If a Pattern Emerged, Document It**

If you discovered something reusable, add it to `Patterns/`:

Example: `Patterns/global_query_filters.md`

```markdown
# Pattern: Global Query Filters for Multi-tenancy

**When to use:** Any time you need to filter database queries by `owner_trainer_id`

**Implementation:**
```csharp
// In ApplicationDbContext.OnModelCreating()
modelBuilder.Entity<Client>()
    .HasQueryFilter(c => c.OwnerTrainerId == _tenantContext.TrainerId);
```

**Why it works:**
- Automatic on every query — no forgotten WHERE clauses
- Injected `ITenantContext` from DI ensures you're always in the right tenant
- Works with EF Core LINQ, testing (just mock the context), and complex queries

**Gotchas:**
- `.IgnoreQueryFilters()` bypasses the filter — only use in admin/logging scenarios
- Must set `_tenantContext` in the constructor, not in `OnModelCreating()` (it runs after)

**Example test:**
```csharp
[Fact]
public async Task GetAllAsync_ReturnsOnlyTenantClients()
{
    var trainerId = Guid.NewGuid();
    var mockContext = new Mock<ITenantContext>();
    mockContext.Setup(x => x.TrainerId).Returns(trainerId);
    
    var clients = await repository.GetAllAsync();
    
    Assert.All(clients, c => Assert.Equal(trainerId, c.OwnerTrainerId));
}
```
```

**Step 4: If You Hit a Gotcha, Document It**

If you hit a mistake or discovered a painful pattern, add to `Gotchas/gotcha_*.md`:

Example: `Gotchas/gotcha_migrations.md`

```markdown
# Gotcha: EF Core Migrations

## The Problem
Never edit a migration `.cs` file after it's been applied to a shared database (e.g., dev/staging).

## Why
- The migration history in `__EfMigrationsHistory` table records that migration as "applied"
- If you edit it, the database and code get out of sync
- Running migrations again fails with "migration already exists" or constraint errors

## The Right Way
1. Created wrong migration? Run `dotnet ef migrations remove` to revert it (only if not yet applied)
2. Already applied? Create a NEW migration with `dotnet ef migrations add FixPreviousMigration`
3. Always generate migrations with `dotnet ef migrations add` — never write SQL by hand

## Command Reference
```bash
# Generate migration from model changes
dotnet ef migrations add AddClientTable --project src/Infrastructure

# Apply all pending migrations
dotnet ef database update --project src/Infrastructure

# Revert to previous migration (only if not shared yet)
dotnet ef migrations remove --project src/Infrastructure

# See migration history
dotnet ef migrations list --project src/Infrastructure
```
```

## Querying Memory: How to Use It

At any point in a session, you can ask:
- "Load my memory" → Reads MEMORY.md and recent sessions
- "Do we have a pattern for X?" → Searches `Patterns/`
- "Have we hit this bug before?" → Checks `Gotchas/`
- "What did we decide about Y?" → Looks in `Architecture/`

The memory system helps you avoid:
- Repeating the same mistake twice
- Over-engineering the same solution twice
- Forgetting why a decision was made
- Getting stuck on a known blocker

## Workflow: Load → Work → Save

```
1. START SESSION
   ├─ Read .claude/memory/MEMORY.md
   ├─ Skim relevant Gotchas/
   └─ Skim last 1-2 session files

2. WORK
   ├─ Code, test, fix, iterate
   ├─ Notice patterns, gotchas, decisions
   └─ Jot notes inline (← I'll handle this at the end)

3. END SESSION
   ├─ Create Sessions/YYYY-MM-DD-topic.md
   ├─ Document what you did, blockers, learnings
   ├─ Add any new patterns to Patterns/
   ├─ Add any new gotchas to Gotchas/
   ├─ Update MEMORY.md with latest status
   └─ Commit with message: "chore: update memory for session YYYY-MM-DD"
```

## Memory File Rules

- **Markdown only.** Plain text, no HTML, no Word docs. Git-friendly.
- **Dated session files.** Format: `YYYY-MM-DD-topic.md`
- **UTF-8 encoding.** Always.
- **Linked, not duplicated.** If something appears in two places, link to the canonical version.
- **Concise but complete.** Assume future-you doesn't remember this project. Explain context.
- **Committed to git.** Memory IS source-controlled. You can blame a decision, revert a note, see history.

## Example: Complete Memory Workflow

**Session 1:**
1. Read MEMORY.md → "Current focus: Domain layer, Sprint 1"
2. Work on entities and tests
3. At end, create `Sessions/2026-07-25-domain.md`
4. Document pattern: "Global Query Filters" in `Patterns/global_query_filters.md`
5. Commit: `chore: update memory for domain session`

**Session 2 (next day):**
1. Read MEMORY.md → "Last session: 2026-07-25-domain.md, Next: Integration tests"
2. Skim `Sessions/2026-07-25-domain.md` to see what was done
3. Check `Patterns/global_query_filters.md` for the exact implementation
4. Write integration tests, reuse the pattern directly
5. At end, create `Sessions/2026-07-26-integration.md`
6. Commit: `chore: update memory for integration session`

Now **Session 3 (1 week later)** the entire context is there, searchable, diffable, and version-controlled.

## Pro Tips

- **Use wikilinks:** In Obsidian syntax, `[[gotcha_multi_tenancy]]` creates a clickable link to another note
- **Tag for search:** Add `#pattern`, `#gotcha`, `#decision`, `#blocker` to make notes searchable
- **Backlinks:** Notes linking to each other create a natural knowledge graph
- **Vault in git:** The entire `.claude/memory/` directory is in your repo. Push to GitHub. It's your institutional memory.

---

**Your memory is your superpower. Use it.**
