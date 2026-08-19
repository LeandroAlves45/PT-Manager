# PT Manager: memória resumida

O código e `.claude/project/` são fontes de verdade. Esta memória regista apenas estado operacional e decisões estáveis.

## Estado do projeto

- Backend alvo em .NET 10 e C# 14 dentro de `backend/`.
- Projetos de produção: Domain, Application, Infrastructure e Api.
- Arquitetura: modular monolith e Clean Architecture. Sem MediatR, AutoMapper, repository genérico ou Unit of Work genérico.
- PostgreSQL com EF Core é a fonte de verdade. A migration
  `20260804163659_InitialCreate` está gerada, aplicada e é imutável.
- A migration `20260814121132_CompleteTrainingPhase2C` foi revista e aplicada
  na base de dados de desenvolvimento em 14 de agosto de 2026.
- Ainda não existe base de dados de produção. Até ser criada e explicitamente
  identificada, migrations e validações de schema aplicam-se apenas a
  desenvolvimento local e a PostgreSQL efémero em Testcontainers.
- `backend-python/` é apenas referência funcional local.
- Sprint 2 Infrastructure e EF Core concluído em 5 de agosto de 2026.
- Gate final: build Release sem warnings, 282 testes de Domain, 21 testes de
  arquitetura e 108 testes PostgreSQL.
- Os 38 testes de jobs e outbox passaram dez execuções consecutivas.
- Format passou e o modelo EF Core não tem alterações pendentes.

## Entrega do Sprint 2 (concluído)

DurableJob/Outbox com claim concorrente, lease, retry e dead letter; suite usa
PostgreSQL real com `MigrateAsync`. Detalhe: `Sessions/2026-08-05-sprint2-completion.md`.

## Nutrição aprovada

- Fórmulas: Harris Benedict revista 1984, Mifflin St Jeor 1990, Cunningham 1980 e Tinsley por peso corporal 2018.
- Cunningham 1980 usa 500 + 22 × FFM derivada do peso e da percentagem de gordura.
- Excluídas Tinsley FFM e Katch McArdle.
- Cálculo automático apenas com idade usada igual ou superior a 18 anos.
- Atividade: sedentary 1,200; lightly_active 1,375; moderately_active 1,550; very_active 1,725; extremely_active 1,900.
- Goal: maintenance usa zero; deficit subtrai magnitude positiva; surplus soma magnitude positiva; target tem de ser positivo.
- Macro modes: percentage, grams_per_kg e manual_grams.
- Percentage exige soma exata de 100,00 e máximo de duas casas.
- Grams per kg define proteína e gordura; hidratos absorvem a energia restante. O mesmo peso tem de ser usado no cálculo e no snapshot.
- Manual grams admite diferença absoluta máxima de 100 kcal pela regra 4/4/9.
- Toda a matemática usa decimal, sem arredondamento intermédio. Outputs finais usam duas casas e AwayFromZero; fator de atividade preserva três casas.
- Food usa gramas por 100 g. Macros individuais entre 0 e 100 e soma máxima 100. Fibra excluída da soma.

## Dados e comportamento aprovados

- Client.BirthDate e Client.Sex obrigatórios através de Value Objects.
- BiologicalSex contém apenas male e female.
- InitialAssessment remove Age e Gender duplicados e adiciona ActivityLevel como sugestão.
- A página da dieta pode sugerir dados do Client e InitialAssessment, mas o trainer edita os valores efetivamente usados.
- CheckIn nunca altera automaticamente uma dieta.
- MealPlan guarda targets relacionais e NutritionCalculationSnapshot JSONB obrigatório, imutável e com schema_version.
- MealPlan deriva os targets do snapshot para impedir divergência.
- O snapshot mantém os valores escolhidos, fórmula, fator, objetivo, modo, inputs e resultados.
- Calculadores são serviços puros do Domain e não precisam de interface nesta fase.

## Contratos e segurança futuros

- Preservar `/api/v1` e JSON `snake_case`.
- Preview não persiste. Escritas recalculam no servidor; frontend nunca envia resultados confiáveis nem snapshot pronto.
- Tenant vem exclusivamente de ITenantContext.
- Problem Details preserva `detail`; usar 400, 404, 409 e 422 conforme a classe de falha.

## Sprint 3 em execução

- Divisão híbrida em 4 fases, plano em `.claude/tasks/todo.md` e sessão em
  `Sessions/2026-08-06-sprint3-planning.md`.
- Fase 1: fundações (FluentValidation core, Result/Error com 7 categorias,
  Exceptions) + Clients completo como feature de referência.
- Fase 2: Nutrition + Training. Fase 3: Sessions + Assessments + Supplements +
  TrainerSettings. Fase 4: Auth contra portas + Billing + Notifications + gate.
- Repositórios por feature (porta + EF Core) apenas com consumidor concreto.
- Testes unitários dentro de cada fase; gate de revisão no fim de cada fase.
- Docs de pseudocódigo em `docs/backend-files/sprint_3/fase_N/`.
- Todo o diretório `docs/` é documentação local deliberadamente ignorada pelo
  Git. Nunca usar `git add -f` para o versionar.
- Existem dois formatos válidos para docs que orientam código: pseudocódigo
  alargado e código C# real. O pedido explícito do utilizador escolhe o formato.
  Em ambos, cada ficheiro real recebe caminho exato, estado, adequação à camada e
  um único bloco contínuo com o conteúdo integral. Código real pode ser gerado
  diretamente de um desenho funcional aprovado, sem pseudocódigo intermédio, e
  deve ser materializado numa cópia temporária para build e testes.
- Entregas com muitos ficheiros usam um índice curto e blueprints divididos
  por responsabilidade. Cada blueprint marca o destino como existente,
  incompleto ou a criar e preserva alterações feitas por outro agente.
- Padrões detalhados: `Patterns/blueprints_pseudocodigo_por_ficheiro.md` e
  `Patterns/blueprints_codigo_real_por_ficheiro.md`.
- 11/08/2026: fase 2 entrou na consolidação documental da persistência da
  nutrição; notas de sessão em `Sessions/2026-08-11-sprint3-phase2-persistence.md`.
- 18/08/2026: revisão dos blueprints do lote_3D (Supplements) corrigiu DRY,
  ORDER BY/índice e sequenciamento entre ficheiros "full replace"; pattern
  `blueprints_codigo_real_por_ficheiro.md` ganhou 3 regras novas (11-13); notas
  em `Sessions/2026-08-18-lote3D-blueprint-review.md`.

## Fase 1 concluída (2026-08-08)

Foundations e Clients (commit `aee7f6d`) — referência para handlers explícitos,
Result/Error, tenant fail-closed, stores compostos. Detalhe:
`Sessions/2026-08-08-sprint3-phase1-completion.md`.

## Lote 2A Nutrition concluído (2026-08-12)

72 ficheiros implementados. Swaps de ordem usam staging na mesma transação
(índices únicos PostgreSQL são imediatos); queries de detalhe ordenam antes da
projeção para garantir tradução pelo EF Core 10. Detalhe:
`Sessions/2026-08-12-sprint3-phase2-lot2a-completion.md`.

## Fase 2C Training concluída (2026-08-14)

Training, migration e gate transversal concluídos. `ExerciseSetLogQueries` usa
projeção traduzível pelo EF Core 10. Migration aplicada usa CRLF (exceção não
semântica aceite pelo utilizador, ficheiro imutável). Detalhe:
`Sessions/2026-08-14-sprint3-phase2c-completion.md`.

## Próxima execução

1. Implementar o Lote 3E TrainerSettings e administração global remanescente.
2. Não gerar migration intermédia; consolidar as alterações no Lote 3F.
3. No Lote 3F, executar as suites PostgreSQL diferidas dos Lotes 3A a 3D e o
   ciclo migrate, rollback e migrate.
4. Manter a migration CompleteTrainingPhase2C imutável.

## Lote 3C Assessments documentado (2026-08-17)

- Contratos e 61 blueprints de ficheiros reais concluídos em
  `docs/backend-files/sprint_3/fase_3/lote_3C/`.
- InitialAssessment é criada e corrigida apenas pelo trainer. Cliente arquivado
  mantém histórico legível, mas não recebe nova avaliação.
- CheckIn é agendado pelo trainer e respondido pelo cliente apenas no dia local.
  O cliente não cria, reage, cancela ou corrige CheckIns.
- WeightKg é obrigatório na resposta. Restantes medidas, body fat, feedback,
  notas e adherence scores são opcionais.
- Resposta exatamente repetida é idempotente. Uma segunda resposta diferente é
  Conflict. CheckIn falhado permanece histórico e exige novo agendamento.
- Estado não é persistido: deriva de RespondedAt, CancelledAt, CheckInDate e dia
  local. Correct preserva CheckInDate, RespondedAt e CreatedAt.
- CheckIn não produz efeitos laterais em Nutrition, MealPlan ou Training.
- Stores usam locks Client seguido de CheckIn. Não existe lock global do trainer,
  evitando serialização desnecessária entre clientes independentes.
- Existem mappers específicos para InitialAssessment e CheckIn; não foi criado
  mapper genérico nem alterada a autorização estável de Sessions.
- Alterações de schema e testes PostgreSQL ficam diferidos para a migration
  consolidada do Lote 3F. Nenhuma migration foi criada ou editada.
- Comentários explicativos nos blueprints não usam etiquetas introdutórias.
- Próximo passo autorizado: materializar o código do Lote 3C pela ordem do índice
  e validar Domain, Application, Architecture e format.
- Revisão sénior (17/08/2026): corrigido bug de compilação real no doc 04
  (`ToResultAsync` inexistente → `ValidateAsync`+`ToApplicationError()`) e
  corrigido `GetMyDueAsync` (doc 06) para um único `Join`. Nomes de
  constraints/índices divergentes do código real são intencionais (full
  replace documentado). `InitialAssessment.cs` real tem edição manual do
  utilizador em curso, não commitada, fora deste fluxo.
- Nota canónica:
  `Sessions/2026-08-17-sprint3-phase3-lot3c-blueprints.md`.

## Lote 3C Assessments concluído no âmbito pré-migration (2026-08-18)

- Os 58 caminhos de produção dos documentos 01 a 08 estão materializados.
- Aprovados 322 testes Domain, 264 Application, 24 Architecture e 15 testes do
  tradutor PostgreSQL. Build Release e format passaram sem warnings ou erros.
- A revisão corrigiu cinco erros de compilação em testes antigos, removeu uma
  query redundante de timezone, corrigiu o código de erro de FitnessLevel vazio
  e completou a matriz unitária de Assessments.
- O EF Core confirmou alterações pendentes no modelo. A migration, os testes
  PostgreSQL de concorrência e o gate completo continuam reservados ao Lote 3F.
- Validação detalhada:
  `docs/backend-files/sprint_3/fase_3/lote_3C/11_lote_3c_avaliacao_final.md`.
- Nota canónica:
  `Sessions/2026-08-18-sprint3-phase3-lot3c-completion.md`.

## Lote 3D Supplements concluído no âmbito pré-migration (2026-08-19)

- Produção e testes dos documentos 00 a 13 materializados; documento 14 regista
  o gate final. Nenhuma migration foi criada ou editada.
- A revisão corrigiu um commit transacional em falta em UpdateInstructions,
  rollbacks com token cancelado e normalização tardia dos snapshots de auditoria.
- `LikeSearchPattern` é agora a implementação única usada por oito queries. Os
  seis consumidores anteriores foram migrados; já não existe a dívida técnica
  registada na nota de follow-up.
- EF Core 10.0.10 e Npgsql 10.0.3 confirmaram por `ToQueryString()` exatamente um
  `EXISTS` sobre `UNION ALL` na verificação das duas fontes de referências.
- Build Release e format passaram sem warnings ou erros. Foram aprovados 325 testes
  Domain, 306 Application, 24 Architecture e 6 testes sem PostgreSQL de
  `Persistence/Common`.
- O restore passou em modo locked. SSH.NET foi fixado em 2026.0.0 para remover a
  vulnerabilidade alta transitiva de Testcontainers; a auditoria terminou limpa.
- Os testes PostgreSQL do lote estão compilados, mas a execução, o plano SQL real
  e a migration consolidada permanecem reservados ao Lote 3F.
- Validação detalhada:
  `docs/backend-files/sprint_3/fase_3/lote_3D/14_lote_3d_avaliacao_final.md`.
- Nota canónica:
  `Sessions/2026-08-19-sprint3-phase3-lot3d-completion.md`.

## Fase 3 desenhada e Lote 3A documentado (2026-08-14)

- Lotes aprovados: 3A Packs, 3B Sessions, 3C Assessments, 3D Supplements, 3E
  TrainerSettings/admin global e 3F migration consolidada.
- ExpectedDurationDays e ExpectedEndDate são expectativas sem expiração rígida.
- ClientSessionPack conclui com saldo zero e CompletedAt; vários packs podem
  estar utilizáveis e o trainer seleciona explicitamente.
- Não atribuir packs a clientes arquivados. Cancel exige saldo integral e zero
  referências a Session. Não existe ajuste manual de saldo.
- Complete e NoShow debitam; Restore repõe quando aplicável; cancelamentos não
  mexem no saldo. Transições repetidas equivalentes são idempotentes.
- Agenda: uma Session Scheduled por cliente/dia local e nenhum intervalo
  sobreposto do trainer.
- Superuser não gere operações funcionais de clientes. Catálogos globais usam
  casos administrativos explícitos. Supplement global usa hard delete apenas
  sem referências e auditoria append-only na mesma transação; Food e Exercise
  ficam para decisão própria no Lote 3E.
- TrainerSettings remove BackgroundImageUrl. Cores null usam o tema default e
  media é gerida por porta + outbox, com Cloudinary concreto no sprint previsto.
- Blueprints e Gate 3A: `docs/backend-files/sprint_3/fase_3/`. Código real
  implementado e aceite em 15/08/2026.

## Lote 3A Packs concluído (2026-08-15)

PackType e ClientSessionPack implementados. Testes PostgreSQL de Packs escritos
mas não executados (`MigrateAsync` bloqueado por pending model changes até ao
Lote 3F) — **não apresentar isto como aprovação PostgreSQL**; no Lote 3F são
obrigatórios suite completa, migrate, rollback e migrate. Detalhe:
`Sessions/2026-08-15-sprint3-phase3-lot3a-completion.md`.

## Lote 3B Sessions concluído (2026-08-17)

43 caminhos materializados. Agenda usa dia local, intervalos semiabertos e
serialização pelo lock do trainer; Complete/NoShow debitam, Restore repõe.
`SessionPersistenceTests` compilados, execução diferida ao Lote 3F. Padrão de
mapper de resultados aplicado a Nutrition/Packs/Training (sete mappers
específicos); Clients ficou explícito por outcomes distintos. Detalhe:
`Sessions/2026-08-16-sprint3-phase3-lot3b-blueprints.md` e
`Sessions/2026-08-17-sprint3-phase3-lot3b-completion.md`.
