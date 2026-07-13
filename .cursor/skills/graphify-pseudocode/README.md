# Graphify Pseudocode Skill

Generates extended pseudocode for Chatbot project components following Clean Architecture patterns and your specific style.

## Quick Start

1. Ensure the project has been analyzed with Graphify:
   ```bash
   graphify . --code-only
   graphify cluster-only .
   ```

2. Use the skill when implementing new features:
   ```
   Feature: SendMessageUseCase
   Context: Application.UseCases
   File location: docs/pseudocode/SendMessageUseCase.md
   Learning focus: Atomic transactions + Result pattern + IAnthropicService
   Dependencies: IConversationRepository, IMessageRepository, IAnthropicService
   ```

3. The skill generates a .md file with:
   - Extended pseudocode following your style
   - XML/JSDoc structure suggestions
   - WHY comments explaining decisions
   - Mentor notes for learning
   - Implementation checklist

## Files

- `SKILL.md` — Skill definition and usage guide
- `scripts/pseudocode_generator.py` — Core generation logic
- `README.md` — This file

## Architecture

The skill:
1. Loads `graphify-out/graph.json` to understand dependencies
2. Reads `GRAPH_REPORT.md` for architecture patterns
3. Extracts golden rules from `claude.md`
4. Generates pseudocode based on the requested feature
5. Saves to `docs/pseudocode/[Feature].md`

## Output Structure

Generated pseudocode includes:
- Objective (what this component does)
- Mentor note (why this pattern matters)
- XML doc structure with examples
- Extended pseudocode (not compressed)
- Implementation notes (testing, error handling, DI)
- Checklist (verification points)
- Next steps

## Style

All pseudocode follows your established patterns:
- Classes and methods in English
- Comments in Portuguese (PT-PT)
- `MÉTODO ASYNC`, `CAMPO PRIVADO SÓ-LEITURA` notation
- `Result<T>` pattern for business failures
- Dependency Injection in constructors
- Atomic database operations
- No hardcoding

## Integration

This skill is designed to work with:
- `graphify` (graph generation)
- Your `claude.md` golden rules
- Clean Architecture structure
- Your existing `05-use-cases.md` style reference
