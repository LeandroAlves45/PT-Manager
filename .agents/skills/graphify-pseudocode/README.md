# Graphify Pseudocode Skill

Skill local do PT Manager para criar blueprints de pseudocódigo alargado por
ficheiro real.

## Contrato de output

Cada target contém:

1. Caminho exato.
2. Estado atual.
3. Adequação à camada.
4. Um único bloco contínuo com o ficheiro completo.
5. Notas de mentor.
6. Validações específicas.

XML Docs ou JSDoc, assinaturas, regras, corpos, falhas e transações ficam no
mesmo bloco. A skill não usa metas globais de cobertura e não escreve migrations
manualmente.

## Gerador opcional

```powershell
python .agents/skills/graphify-pseudocode/scripts/pseudocode_generator.py `
  --feature CreateMealPlanHandler `
  --layer Application `
  --file-path backend/src/Application/Features/Nutrition/CreateMealPlan/CreateMealPlanHandler.cs `
  --state "to create"
```

Sem `--output`, o documento é enviado para stdout. Com `--output`, o destino é
sempre explícito.

O skeleton gerado deve ser completado depois de inspecionar o código e as fontes
canónicas do projeto.
