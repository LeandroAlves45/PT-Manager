---
name: obsidian-ptmanager
description: Maintain persistent PT Manager project memory across Codex and Claude. Use at session start, milestones, material architectural decisions, reviews, retrospectives, or when the user asks to load or update memory. Reads `.codex/memory/MEMORY.md` and `.claude/memory/MEMORY.md`, keeps concise canonical milestone notes, and never replaces current code or project documentation as source of truth.
---

# Persistent memory for PT Manager

Use memory to preserve durable context, not as a second specification.

## Source precedence

When sources disagree, use this order:

1. Current code and schema.
2. Canonical documents under `.claude/project/`.
3. `AGENTS.md`.
4. `.codex/memory/MEMORY.md` and `.claude/memory/MEMORY.md`.
5. Historical session notes.

State the contradiction and correct stale memory instead of adapting code to it.

## Start of work

1. Read `.codex/memory/MEMORY.md` when it exists.
2. Read `.claude/memory/MEMORY.md` when it exists.
3. Read only recent session, pattern or gotcha notes relevant to the request.
4. Run `git status --short` before planning edits.

Never read protected secret files while loading context.

## When to write memory

Update memory only when at least one condition is true:

1. A sprint, phase or major milestone changed state.
2. A material architecture, contract, security or data decision was approved.
3. A reusable pattern or recurring gotcha was discovered.
4. The user explicitly requested a memory update.

Do not create a session file for routine conversations, status checks or changes
already captured by an existing canonical note.

## Writing workflow

1. Verify the claim against code, tests, git history or canonical docs.
2. Create one dated note under `.claude/memory/Sessions/` only for a milestone or
   decision that needs its own history.
3. Link both memory indexes to that note when both need the same context.
4. Keep indexes concise: current state, durable decisions, limitation and next step.
5. Link instead of copying a long narrative into multiple files.
6. Record environmental test blockers separately from functional failures.
7. Never claim a test passed without evidence from that execution or an explicitly
   attributed prior review.

## Patterns and gotchas

Create or update a file in `Patterns/` when the rule should guide future work.
Create or update a file in `Gotchas/` when a verified failure is likely to recur.
Do not duplicate a rule already enforced by `AGENTS.md` unless the memory adds
project-specific evidence or a concrete example.

## Content rules

Use UTF-8 Markdown and Portuguese from Portugal. Include exact dates, paths and
commit identifiers where verified. Never store credentials, tokens, connection
strings or contents from protected files.
