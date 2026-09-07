# Estado ativo: Sprint 4 FINALIZADO

Atualizado: 2026-09-06
Próximo passo: planear o primeiro slice do Sprint 5 antes de alterar código

## Estado em uma linha

As Fases 1 a 6 do Sprint 4 estão implementadas, revistas e validadas. Não existem
blockers backend pendentes deste sprint.

## Evidência final

1. Build Release: 0 avisos e 0 erros.
2. Suite integral: 1907 aprovados, 1 ignorado manual, 0 falhas.
3. Domain 421, Application 503, Infrastructure 406, API 538 e Architecture 39.
4. Format check aprovado sem alterações.
5. EF Core sem pending model changes.
6. Snapshot OpenAPI com 135 operações e sem query parameters auxiliares ou fora de snake_case.
7. Dependências dos nove projetos sem vulnerabilidades reportadas pelo feed NuGet.

## Correções da Fase 6

1. `PageParameters` deixou de expor propriedades auxiliares ao ApiExplorer.
2. `ExternalAuthenticationStore` suporta retry e confirmação de commit ambígua sem duplicados.
3. Eventos de segurança do plano usam logging estruturado sem credenciais ou tokens.
4. QG3-REF-001 tem corrida concorrente provada em PostgreSQL real.

## Trabalho diferido

`QG5-FRONTEND-001` continua aberto e é obrigatório no primeiro slice que alterar
React, previsivelmente no Sprint 5 ou 6. Não está atribuído a um sprint específico
até o respetivo planeamento ser aprovado.

## Ler nesta ordem

1. Este ficheiro.
2. `.claude/memory/Sessions/2026-09-06-sprint4-finalizado.md`.
3. `docs/backend-files/sprint_4/Validacao_Final_Sprint_4.md`.
4. `.claude/project/02_SPRINTS_ROADMAP.md`.
5. `backlogs/QualityGates.md`.
