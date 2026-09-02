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
graphify explain "AuthController"
graphify explain "JwtAccessTokenIssuer"
```

Confirmar símbolos do sprint actual presentes. Comparar `nodes` em `graph.json`
(deve crescer após sprints grandes; 2026-09-02: **9262 nós**, 508 comunidades).

## Uso pelas IAs

| Serve para | Não serve para |
|---|---|
| Impacto («se altero X, o que toco?») | Estado do sprint |
| Descobrir handlers/stores relacionados | Decisões de negócio |
| Validar ordem de implementação | Contratos HTTP |

**Regra:** se `graph.json` for mais antigo que o fecho do Sprint anterior, **ignorar**
e usar `surface.yaml` do Sprint Pack activo.

## Última geração

2026-09-02 — **9262 nós**, 508 comunidades. Inclui Sprint 3 completo e Sprint 4
(Fases 1–3: Auth, moderação, interceptors). `graph.html` não regenerado (>5000 nós).

## Opcional (futuro)

Extrair de `GRAPH_REPORT.md` um resumo curto versionado, por exemplo
`.claude/project/sprints/GRAPH_SUMMARY.md`, no fecho de cada Sprint.
