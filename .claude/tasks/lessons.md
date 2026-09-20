# Lessons

Lições capturadas depois de correções do utilizador.

## 2026-09-18 — Sprint 6B (planeamento e blueprints)

1. **Enums em query string ligam pelo nome do membro.** `?status=pending_review` devolve 400
   mesmo com `JsonStringEnumConverter(SnakeCaseLower)` configurado (isso só afeta o corpo JSON).
   Filtros novos: membros de uma palavra, e um teste funcional que prova 200 no válido e 400 no
   inválido.
2. **O EF Core não ordena por membro de um record construído na projeção.** Projetar para tipo
   anónimo, ordenar em SQL e construir o record em memória. Só se apanha a correr contra
   PostgreSQL real.
3. **Filtro explícito de tenant numa query é defesa em profundidade.** Uma mutação que o desligue
   pode sobreviver porque o Global Query Filter já isola. Mutações de isolamento têm de desligar
   a camada que isola de facto.
4. **Ficheiros grandes vão para blueprint por excerto** (> 200 linhas): âncora com contexto
   inalterado, bloco completo, marcadores `// [fase] NOVO/ALTERADO/REMOVIDO` e validação por
   inclusão literal no ficheiro materializado.
