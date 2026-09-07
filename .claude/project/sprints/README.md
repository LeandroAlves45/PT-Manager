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

1. Ler `.claude/memory/ACTIVE.md` — fase activa e ordem de leitura.
2. Se existir pack activo, ler o `README.md` desse pack.
3. Ir a `docs/backend-files/...` só quando fores gerar ou implementar um documento concreto.
4. **Não** reler `00_ARCHITECTURE.md` completo se a secção relevante já estiver referenciada.

## Fonte canónica

- Arquitectura estável: `.claude/project/00…03` (não duplicar em `.cursor/project/`).
- Estado operacional: `.claude/memory/MEMORY.md` + `ACTIVE.md`.
- Evidência por marco: `.claude/memory/Sessions/`.
- Grafo de dependências: `graphify-out/` + [GRAPHIFY.md](GRAPHIFY.md).

## Packs disponíveis

| Sprint | Fases | Estado | Pack |
|---|---|---|---|
| 4 | 1–5 | **Fechado no backend** (2026-09-06) | Sem pack activo — ver `ACTIVE.md` e `Sessions/2026-09-06-sprint4-fase5-migration-local-aplicada.md` |
| 5 | — | Por planear | Criar pack quando a fase activa for definida |

O pack `sprint-4/fase-4/` foi **removido** porque estava desactualizado (dizia «não
implementada» com a Fase 4 já fechada). Não duplicar estado de sprint em packs históricos;
usar notas de sessão e `docs/backend-files/sprint_4/`.

## Graphify

Regenerado em **2026-09-06** no fecho do Sprint 4: **4211 nós**, 250 comunidades.
Detalhe em [GRAPHIFY.md](GRAPHIFY.md).
