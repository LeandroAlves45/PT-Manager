# Graphify — índice de dependências

## Quando regenerar

**Uma vez por Sprint fechado**, não por fase intermédia. Correr antes de planear o
Sprint seguinte ou quando a Fase activa mexer em >30 ficheiros de produção.

## Comandos (graphify 0.9.x)

Na raiz do repositório:

```powershell
# 1. Extracção AST do código (sem LLM, sem API key)
graphify extract . --code-only --no-cluster

# 2. Clustering + GRAPH_REPORT.md
graphify cluster-only . --no-label

# Opcional: nomes de comunidades com LLM (requer API key)
graphify label .
```

Saída principal: `graphify-out/graph.json`, `graphify-out/GRAPH_REPORT.md`.

**Nota:** com >5000 nós, `graph.html` não é gerado (limite do graphify). Usar
`GRAPH_REPORT.md`, `graphify explain "X"` ou `graphify affected "X"`.

Backup automático da geração anterior em `graphify-out/YYYY-MM-DD/`.

## Validação rápida pós-geração

```powershell
graphify explain "GoogleAuthController"
graphify explain "ExternalAuthenticationStore"
graphify explain "AuthController"
```

Confirmar símbolos do sprint actual presentes. Comparar `nodes` em `graph.json`
após sprints grandes.

## Uso pelas IAs

| Serve para | Não serve para |
|---|---|
| Impacto («se altero X, o que toco?») | Estado do sprint |
| Descobrir handlers/stores relacionados | Decisões de negócio |
| Validar ordem de implementação | Contratos HTTP |

**Regra:** se `graph.json` for mais antigo que o fecho do Sprint anterior, **ignorar**
e usar documentação canónica em `.claude/project/` + `ACTIVE.md`.

## Última geração

**2026-10-22** (fecho do Sprint 6): **15 535 nós**, 1146 comunidades e 31 686 arestas, com backend e frontend.
- Inclui o Sprint 6 completo: layout, portal do cliente, 6GA Auth UX e as três fases da 6GB.
- Símbolos de controlo da última fase: `MyMealPlanSelectionStore`, `PortalCard`.
- Ficaram sem nós 4 ficheiros de configuração: `hooks.json`, `global.json` e os dois `appsettings*.json`.
- A geração anterior está guardada em `graphify-out/2026-10-22/`.

Geração anterior: **2026-09-06**, com **4211 nós**, 250 comunidades e 7907 arestas (graphify 0.9.12).

Inclui Sprint 4 completo no backend: Auth local, moderação, controllers de negócio,
Client Portal e **Google Sign-In (Fase 5)** — `GoogleAuthController`,
`ExternalAuthenticationStore`, `GoogleExternalIdentityVerifier`, migration
`AddExternalIdentities`.

`graph.html` regenerado (<5000 nós). Backup anterior em `graphify-out/2026-09-06/`
(geração de 2026-09-02: 9262 nós, 508 comunidades — métricas não comparáveis
directamente entre extracções incrementais).

## Opcional (futuro)

Extrair de `GRAPH_REPORT.md` um resumo curto versionado, por exemplo
`.claude/project/sprints/GRAPH_SUMMARY.md`, no fecho de cada Sprint.
