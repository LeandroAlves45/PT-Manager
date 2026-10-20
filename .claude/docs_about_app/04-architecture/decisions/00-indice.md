# Decisões

Estado em 2026-10-15: não há ADR nesta pasta.

As decisões de arquitectura do MVP estão em `.claude/project/00_ARCHITECTURE.md`, secções 2 a 17. As de esquema estão em `.claude/project/01_DATABASE_SCHEMA.md`. As de produto estão nas `RN-`, nas `RNF-` e no plano `docs/blueprints/frontend-files/sprint_6/sprint_6G/00_plano_sprint_6G.md`.

Não se abre um ADR por regra de ecrã. O modelo `_modelo.md` fica para uma decisão que não caiba nesses sítios.

Diferenças já conhecidas, sem ADR próprio:

| Tema | Onde está a decisão | Porque não é um ADR aqui |
|---|---|---|
| Reembolso | `RNF-014` | Sem prioridade. A arquitectura ainda não o inclui. |
| Intervalo de repetições | Plano 6G, ponto 5; `business-rules.md` | Muda `exercise_sets` na 6GB-2. O esquema actual tem um só `planned_reps`. |
| Momento do suplemento vazio | Plano 6G, ponto 3 | Aplicado na 6GB-SU (2026-10-19): `timing` opcional no suplemento e na atribuição. |
| Marca na área do treinador | Plano 6G, ponto 14 | Os dados já estão em `trainer_settings`. Muda o ecrã, na 6GB-2. |
| Copiar link do convite | Plano 6G, ponto 13 | O token persiste só como hash. |
