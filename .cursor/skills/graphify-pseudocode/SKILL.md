---
name: graphify-pseudocode
description: Generate or review extended pseudocode blueprints for PT Manager files in Domain, Application, Infrastructure, Api, frontend and tests. Use when the user asks for files.md, pseudocode, implementation guides, feature blueprints, handlers, stores, queries, entities, controllers or tests. Enforces one continuous complete blueprint per real target file with exact path, XML Docs or JSDoc, comments explained, mentor notes and validations.
---

# Graphify Pseudocode for PT Manager

Generate documentation that lets another developer implement a target file without
searching for missing signatures, rules or method bodies.

## Required preparation

1. Read `AGENTS.md`, `.claude/memory/ACTIVE.md`, `.claude/memory/MEMORY.md` and the
   relevant Sprint Pack under `.claude/project/sprints/`.
2. Inspect the real code, tests and `git status --short` before describing a file.
3. Read `.claude/memory/Patterns/blueprints_pseudocodigo_por_ficheiro.md`.
4. Treat `backend-python/` only as optional flow discovery. Never use it as the
   target contract or architecture.
5. Prefer the actual source graph and code. Use `graphify-out/` only when current
   (see `.claude/project/sprints/GRAPHIFY.md` and `graphify-pseudocode/README.md`).
   The skill must still work without it.

## Mandatory structure per target file

For every production or test file, emit in this order:

1. Exact repository-relative target path.
2. Current state: `existing`, `incomplete` or `to create`.
3. Layer fit and one clear responsibility.
4. One continuous fenced block containing the complete extended pseudocode file.
5. Mentor notes after the block.
6. File-specific validations after the mentor notes.

The continuous block must include, when applicable:

1. All imports or `using` directives.
2. Namespace.
3. XML Docs for public C# types and members, or JSDoc for public frontend APIs.
4. Type declaration, inheritance, fields, properties and constructor.
5. Every public and private method required by the responsibility.
6. Complete branches, mapping, failures and return values.
7. Transaction, concurrency, idempotency and cancellation behavior.
8. Comments explained immediately beside non-obvious decisions.

Use English for identifiers. Use Portuguese from Portugal for XML Docs, JSDoc,
Comments explained, mentor notes and explanations.

## Prohibited output

Do not:

1. Separate XML Docs, signatures, business rules and method bodies into different
   sections.
2. List method names without complete behavior.
3. Describe tests only by name. Include Arrange, Act and Assert in each test.
4. Use generic paths or placeholders that leave the destination undecided.
5. Invent types, packages or contracts without checking the repository.
6. Use exceptions for expected Application flow when Result is the project pattern.
7. Set a global line-coverage percentage. Cover the critical behavior and failure
   scenarios required by the feature.
8. Write real migration classes by hand. Document model configuration changes and
   commands that generate the migration.

## Large deliveries

Create a short index and split blueprints by responsibility, such as contracts,
validation, handlers, persistence and tests. Do not repeat full pseudocode in the
index. Keep one public C# type per target file where that is the project convention.

Mark dependencies and gates between document batches. Do not document a later batch
as implemented before its prerequisite gate is approved.

## Testing requirements

1. Domain tests use no mocks and verify invariants.
2. Application tests use small fakes or mocks for ports and verify orchestration.
3. Infrastructure tests use PostgreSQL Testcontainers for EF Core, SQL,
   constraints, rollback and concurrency.
4. Include negative cross-tenant cases for tenant-owned features.
5. Verify `CancellationToken` propagation on asynchronous I/O.
6. Test behavior and failure outcomes, not private implementation details.

## Bundled generator

Use `scripts/pseudocode_generator.py` when a deterministic skeleton is useful. It
accepts the real target path and can print to stdout or write to an explicit output.
The generated skeleton is only a starting point: replace every placeholder after
inspecting the repository.

Example:

```powershell
python .claude/skills/graphify-pseudocode/scripts/pseudocode_generator.py `
  --feature CreateMealPlanHandler `
  --layer Application `
  --file-path backend/src/Application/Features/Nutrition/CreateMealPlan/CreateMealPlanHandler.cs `
  --state "to create"
```

## Final quality gate

Before delivery, verify that every referenced target path is exact, every file has
one continuous complete block, all expected failures appear inside the responsible
method, and the implementation order matches Clean Architecture dependencies.
