# Estado ativo: Sprint 5A PLANEADO

Atualizado: 2026-09-07
Próximo passo: aplicar os blueprints da Fase 5A ao backend real e implementar a
matriz de testes antes de activar QStash

## Estado em uma linha

O Sprint 4 está finalizado. O plano geral do Sprint 5 e os blueprints de produção da
Fase 5A estão concluídos; a implementação real ainda não começou.

## Evidência documental

1. Plano geral dividido em 5A dispatcher, 5B Stripe, 5C imagens e 5D vídeo.
2. Único job real 5A: `send_notification` versão 1.
3. Único template real 5A: `session_reminder` com quatro campos fechados.
4. Quarenta e quatro ficheiros dos blueprints materializados numa cópia temporária.
5. Build Release da cópia: zero avisos e zero erros.
6. Nenhum ficheiro real de backend, teste ou migration foi alterado.
7. Format da cópia aprovado; testes, migration e gates de runtime permanecem
   abertos.

## Bloqueios de rollout

1. `QStash:Enabled` permanece `false` até aplicação, testes e migration.
2. O preflight tem de encontrar zero itens não concluídos ou em `dead_letter` dos
   tipos `billing_notification` e `trainer-logo.delete`, porque os handlers pertencem
   às Fases 5B e 5C.
3. Não existe job global na allowlist inicial.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-07-sprint5-planeamento-fase5a-blueprints.md`.
3. `docs/backend-files/sprint_5/Plan_sprint_5.md`.
4. `docs/backend-files/sprint_5/sprint_5A/00_indice_ordem_decisoes_e_gates.md`.
5. Documentos 01 a 14 da Fase 5A, pela ordem numérica.
6. `backlogs/QualityGates.md`, secção Sprint 5 Fase 5A.
