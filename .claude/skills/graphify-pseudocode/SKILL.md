---
name: graphify-pseudocode
description: |
  Generate extended pseudocode for features in PT Manager following Clean Architecture organized by feature (Api → Application → Domain ← Infrastructure). Generates detailed pseudocode with XML doc/JSDoc suggestions, essential comments, and mentor notes for learning. Use when implementing features, handlers, repositories, entities, or API controllers.
---

# Graphify Pseudocode Generator

Generate detailed, educational pseudocode for PT Manager (ASP.NET Core/.NET 10 + React SaaS multi-tenant) that follows the project's exact style and Clean Architecture patterns.

## What This Skill Does

When you need to implement a feature, this skill:
1. Analyzes your project graph to understand context and dependencies
2. Reads your golden rules (Clean Architecture, DI, security, testing patterns)
3. Generates pseudocode in your established style (structured like `05-use-cases.md`)
4. Includes XML/JSDoc structure suggestions
5. Adds WHY comments explaining technical decisions
6. Includes mentor notes for learning
7. Creates a .md file ready to guide implementation

## When to Use This Skill

**Triggers:**
- "Generate pseudocode for [FeatureName]"
- "Create a blueprint for [UseCase/Repository/Entity/Endpoint]"
- "I need pseudocode for implementing [Component] in [Layer]"
- "Design [Feature] following Clean Architecture"
- "What should [Module] look like? (structured pseudocode)"

**Best for:**
- New use cases (Application layer)
- Repository implementations (Infrastructure layer)
- Domain entities with business logic
- API endpoints (Api layer)
- Any component where you want to learn the pattern while building

## How to Request Pseudocode

Provide these details (at minimum):

```
Feature/Component: [Name]
Context/Layer: [Application.UseCases | Domain.Entities | Infrastructure.Repositories | Api.Endpoints]
File location: docs/pseudocode/[NameFeature].md
Learning focus: [List topics you want highlighted — e.g., "DI + Result pattern", "Repository pattern + EF Core", "Transaction atomicity"]
Dependencies: [What this connects to — e.g., "IConversationRepository, IMessageRepository, IAnthropicService"]
Additional context: [Optional — any specific patterns or gotchas to emphasize]
```

## Example Input

```
Feature: CreateMealPlanHandler
Context: Application.Features.Nutrition
File location: docs/pseudocode/CreateMealPlanHandler.md
Learning focus: Result pattern + multi-tenant ownership check + explicit mapping (no AutoMapper)
Dependencies: IMealPlanRepository, IClientRepository, ITenantContext, MealPlan, Client
Additional context: Should reject clients that don't belong to the current trainer, explain why SaveChangesAsync happens once at the end
```

## Output Structure

The generated .md file follows this structure:

```
# [Feature Name] Implementation Guide

## Objective
[What this component does]

## Why This Matters (mentor note)
[Learning context and architectural importance]

## [Component 1] — [Main Responsibility]

### XML/JSDoc Structure
[Exact structure to use with explanations]

### Pseudocode
[Extended pseudocode in your style]

### Essential Comments
[Explanations of technical decisions]

## [Component 2] — [If multiple components]
[Same pattern]

## Implementation Notes
[XML doc patterns, comment guidelines, error handling, testing considerations]

## Checklist
[Step-by-step verification points]

## Next Steps
[Links to related documentation or follow-up tasks]
```

## What You'll Get

1. **Extended pseudocode** — Not compressed, reads like a technical tutorial
2. **XML doc suggestions** — Exact `<summary>` tags and structure for C# classes
3. **WHY comments** — Explaining decisions (atomicity, dependency injection, error handling)
4. **Mentor notes** — Teaching moments explaining patterns and gotchas
5. **Dependency mapping** — Shows how this component connects via the graph
6. **Checklist** — Verification points before considering implementation complete
7. **Clean Architecture alignment** — Every component respects layer boundaries and DI

## Style Reference

The pseudocode follows your established patterns from `05-use-cases.md`:
- Classes and methods in English
- Comments in Portuguese (PT-PT)
- `MÉTODO ASYNC`, `CAMPO PRIVADO SÓ-LEITURA`, `Result<T>.Success/Failure` patterns
- Dependency Injection in constructors
- No hardcoding, no direct exception handling in use cases (delegation to middleware)
- Atomic database operations
- Comprehensive, educational tone

## Graph Integration

This skill automatically:
- Loads your `graph.json` to understand dependencies
- Reads `GRAPH_REPORT.md` for architecture patterns
- Identifies "God Nodes" (core abstractions)
- Maps your component to its community in the graph
- Suggests connections based on your actual codebase

## Project Context

Your project structure:
- **Domain Layer**: Pure business logic (entities, interfaces)
- **Application Layer**: Orchestration (use cases, DTOs, services)
- **Infrastructure Layer**: Persistence & external services (repositories, DB, API clients)
- **Api Layer**: HTTP contracts (endpoints, filters, middleware)

The skill ensures pseudocode respects these boundaries.

## Security & Best Practices

Generated pseudocode includes:
- No hardcoded credentials or secrets
- Parameterized queries via EF Core
- Proper error handling patterns
- Input validation references (FluentValidation)
- Secure patterns for external service calls

## Testing Mindset

Each pseudocode component includes notes on:
- What should be unit tested (Domain logic)
- What needs mocks (Application layer repositories)
- Integration test scenarios
- Coverage goals (80%+ target)

---

## Next: Use This Skill

Ready? Provide your feature details using the format above, and the skill will generate your pseudocode file. The output .md will live in `docs/pseudocode/` and will be ready to guide your implementation.
