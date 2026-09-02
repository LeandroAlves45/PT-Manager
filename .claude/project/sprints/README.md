# Sprint Packs — índice de navegação para IAs

Estes ficheiros são **versionados no Git** e servem de mapa. Os blueprints com código
integral ficam em `docs/backend-files/` (local, gitignored).

## Estrutura

```
.claude/project/sprints/
├── README.md              ← este ficheiro
├── GRAPHIFY.md            ← quando regenerar o grafo
└── sprint-N/
    └── fase-M/
        ├── README.md      ← scope, ordem, gates, blockers
        └── surface.yaml   ← paths e contagens (opcional, machine-readable)
```

## Como usar (qualquer agente)

1. Ler `.claude/memory/ACTIVE.md` — aponta para o pack activo.
2. Ler o `README.md` do pack activo.
3. Ir a `docs/backend-files/...` só quando fores gerar ou implementar um documento concreto.
4. **Não** reler `00_ARCHITECTURE.md` completo se o pack já referencia a secção relevante.

## Fonte canónica

- Arquitectura estável: `.claude/project/00…03` (não duplicar em `.cursor/project/`).
- Estado operacional: `.claude/memory/MEMORY.md` + `ACTIVE.md`.
- Evidência por marco: `.claude/memory/Sessions/`.

## Packs disponíveis

| Sprint | Fase | Estado | Pack |
|---|---|---|---|
| 4 | 4 | Documentada, por implementar | [fase-4/README.md](sprint-4/fase-4/README.md) |

Quando uma fase fechar, actualizar o pack e mover o ponteiro em `ACTIVE.md`.
