# Graphify Pseudocode Skill

Skill do PT Manager para blueprints de pseudocódigo alargado por ficheiro real.
Complementa `sprint-context` (modo `blueprint`).

## Preparação obrigatória

1. `.claude/memory/ACTIVE.md` e Sprint Pack activo
2. Código real e `git status --short`
3. `.claude/memory/Patterns/blueprints_pseudocodigo_por_ficheiro.md` (ou
   `blueprints_codigo_real_por_ficheiro.md` se pedido código integral)
4. `graphify-out/` **só se actualizado** (ver abaixo)

## Regenerar o grafo (graphify 0.9.x)

**Quando:** fecho de cada Sprint, não por fase intermédia.  
**Onde:** raiz do repositório. Detalhe completo em `.claude/project/sprints/GRAPHIFY.md`.

```powershell
# 1. Extracção AST (sem LLM, sem API key)
graphify extract . --code-only --no-cluster

# 2. Clustering + GRAPH_REPORT.md
graphify cluster-only . --no-label

# Opcional: nomes de comunidades com LLM
graphify label .
```

**Saída:** `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`.  
Com >5000 nós, `graph.html` não é gerado — usar `graphify explain "X"`.

**Validação rápida:**

```powershell
graphify explain "AuthController"
graphify explain "JwtAccessTokenIssuer"
```

Última geração conhecida: 2026-09-02 (9262 nós, 508 comunidades).

## Contrato de output

Cada target contém:

1. Caminho exacto a partir da raiz do repo
2. Estado: `existing`, `incomplete` ou `to create`
3. Camada e responsabilidade
4. Um único bloco contínuo com o ficheiro completo
5. Notas de mentor
6. Validações específicas

XML Docs (C#) ou JSDoc (frontend), assinaturas, regras, corpos, falhas e
transações ficam no **mesmo bloco**. Sem migrations escritas à mão.

## Uso do grafo na skill

| Serve para | Não serve para |
|---|---|
| Dependências entre handlers/stores | Estado do sprint |
| Impacto de alteração | Decisões de negócio |
| Ordem relativa de ficheiros | Substituir leitura do código real |

Se `graph.json` for anterior ao último fecho de Sprint, **ignorar** e usar
`surface.yaml` do Sprint Pack.

## Gerador opcional (skeleton)

```powershell
python .cursor/skills/graphify-pseudocode/scripts/pseudocode_generator.py `
  --feature CreateMealPlanHandler `
  --layer Application `
  --file-path backend/src/Application/Features/Nutrition/CreateMealPlan/CreateMealPlanHandler.cs `
  --state "to create"
```

Sem `--output` → stdout. Com `--output` → destino explícito.  
Completar sempre com inspecção do código e fontes canónicas.

## Ficheiros

- `SKILL.md` — definição e regras de output
- `scripts/pseudocode_generator.py` — skeleton opcional
- `README.md` — este ficheiro

## Integração

- `sprint-context` — bootstrap e modos plan/blueprint/review
- `AGENTS.md` — arquitectura e contrato HTTP
- Blueprints integrais → `docs/backend-files/` (local, gitignored)
