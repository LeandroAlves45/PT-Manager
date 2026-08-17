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

## Entrega do Sprint 2

- Diretório consolidado: `docs/backend-files/sprint_2/`.
- Os pacotes adjust_db, final_adjustments, newTables e sprint_2_tests foram
  aplicados e mantidos como histórico técnico.
- A suite usa PostgreSQL 17 real com Testcontainers e `MigrateAsync`, sem
  `EnsureCreatedAsync`.
- As oito relações cross-tenant críticas são protegidas por FKs compostas e
  testadas diretamente na base de dados.
- DurableJob e Outbox possuem claim concorrente, owner de lease, recuperação,
  retry, dead letter e idempotência.
- Retries com data presente ou passada são rejeitados pelos stores.

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
- Formato obrigatório para docs que orientam código: cada ficheiro real recebe
  caminho exato, adequação à camada e um único bloco contínuo com o ficheiro
  completo em pseudocódigo alargado (`using`, namespace, XML Docs, campos,
  constructor, métodos e lógica). Não separar assinaturas, regras e corpos em
  blocos ou secções diferentes. Notas de mentor e validações ficam depois.
- Entregas com muitos ficheiros usam um índice curto e blueprints divididos
  por responsabilidade. Cada blueprint marca o destino como existente,
  incompleto ou a criar e preserva alterações feitas por outro agente.
- Padrão detalhado: `Patterns/blueprints_pseudocodigo_por_ficheiro.md`.
- 11/08/2026: fase 2 entrou na consolidação documental da persistência da
  nutrição; notas de sessão em `Sessions/2026-08-11-sprint3-phase2-persistence.md`.

## Fase 1 concluída (2026-08-08)

- Foundations e Clients foram concluídos no commit `aee7f6d` e revistos.
- A Fase 1 passou a ser a referência para handlers explícitos, validators,
  Result/Error, tenant fail-closed, stores compostos, queries projetadas e
  testes Application/PostgreSQL.
- Build Release: zero warnings e zero erros.
- Testes confirmados nesta transição: 282 Domain, 83 Application e 21
  Architecture.
- Format passou e o modelo EF Core não tinha alterações pendentes.
- A repetição dos testes PostgreSQL nesta sessão ficou bloqueada por Docker
  indisponível. A conclusão integral anterior foi confirmada pelo utilizador;
  não registar esta limitação ambiental como falha funcional.
- Nota canónica:
  `Sessions/2026-08-08-sprint3-phase1-completion.md`.

## Lote 2A Nutrition concluído (2026-08-12)

- Os 72 ficheiros alvo de Nutrition estão implementados em Domain,
  Application, Infrastructure e testes.
- Evidência focalizada: 59 testes Domain Nutrition, 26 Application Nutrition e
  24 PostgreSQL Nutrition aprovados.
- Gate completo: 603 testes aprovados, distribuídos por 288 Domain, 109
  Application, 185 Integration e 21 Architecture. `Api.FunctionalTests` ainda
  não possui testes descobertos.
- Build Release passou com zero warnings, format passou e o modelo EF Core não
  tem alterações pendentes.
- Swaps de ordem usam staging dentro da mesma transação porque os índices únicos
  PostgreSQL são imediatos. Queries de detalhe ordenam antes da projeção para
  garantir tradução pelo EF Core 10.
- Training, Gate 2B, Lote 2C e o gate final da Fase 2 foram concluídos em
  14 de agosto de 2026.
- Nota canónica:
  `Sessions/2026-08-12-sprint3-phase2-lot2a-completion.md`.

## Fase 2C Training concluída (2026-08-14)

- A implementação Training, a migration e o gate transversal estão concluídos.
- Aprovados 297 testes Domain, 126 Application, 204 Integration e 24
  Architecture, mais o teste específico de migrate, rollback e migrate.
- `ExerciseSetLogQueries` usa projeção traduzível pelo EF Core 10 e mantém a
  execução no PostgreSQL.
- O modelo EF Core não tem alterações pendentes e as duas migrations constam da
  base de dados de desenvolvimento.
- A migration aplicada usa CRLF. O utilizador aceitou esta exceção não semântica
  e o ficheiro permaneceu imutável.
- Nota canónica:
  `Sessions/2026-08-14-sprint3-phase2c-completion.md`.

## Próxima execução

1. Implementar o Lote 3C Assessments.
2. Não gerar migration intermédia; consolidar as alterações no Lote 3F.
3. Aplicar o padrão reforçado de validação executável aos blueprints futuros.
4. Manter a migration CompleteTrainingPhase2C imutável.

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
  casos administrativos explícitos; delete é soft delete apenas sem referências.
- TrainerSettings remove BackgroundImageUrl. Cores null usam o tema default e
  media é gerida por porta + outbox, com Cloudinary concreto no sprint previsto.
- Blueprints e Gate 3A: `docs/backend-files/sprint_3/fase_3/`. Código real
  implementado e aceite em 15/08/2026.

## Lote 3A Packs concluído (2026-08-15)

- PackType e ClientSessionPack estão implementados em Domain, Application,
  Infrastructure e testes, incluindo regressões de Clients.
- Build Release aprovado sem warnings. Aprovados 310 testes Domain, 155
  Application e 24 Architecture. Format aprovado.
- Os 12 testes PostgreSQL de Packs estão implementados, mas não executaram:
  `MigrateAsync` é bloqueado pelo EF Core 10 enquanto existem pending model
  changes. A migration mantém-se no Lote 3F por decisão explícita.
- Não apresentar esta limitação como aprovação PostgreSQL. No Lote 3F são
  obrigatórios suite completa, migrate, rollback e migrate.
- A revisão corrigiu outcomes, idempotência, mapping de Clients e separou as
  constraints de ordem de datas e coerência de CompletedAt.
- Os blueprints tinham inconsistências materiais apesar da validação estrutural.
  O padrão persistente passou a exigir compilação, testes materializados e gates
  executáveis antes de declarar um pacote pronto.
- Nota canónica:
  `Sessions/2026-08-15-sprint3-phase3-lot3a-completion.md`.

## Lote 3B Sessions concluído (2026-08-17)

- Os 43 caminhos planeados estão materializados em Domain, Application,
  Infrastructure e testes.
- Mutações são exclusivas do trainer; leitura e escrita preservam isolamento
  cross-tenant e cliente arquivado não recebe novas Sessions.
- Agenda usa dia local, intervalos semiabertos e serialização pelo lock do
  trainer. Complete e NoShow debitam; Restore repõe quando aplicável.
- Corrigidas idempotência terminal, fronteira temporal estritamente futura,
  nomes de CompleteSession e tradução da constraint de agenda.
- Build Release aprovado sem warnings. Aprovados 317 testes Domain, 181
  Application, 24 Architecture e 11 testes do tradutor PostgreSQL. Format
  aprovado.
- Os 19 métodos de `SessionPersistenceTests` estão implementados e compilados,
  mas a execução permanece diferida até à migration consolidada do Lote 3F.
- `has-pending-model-changes` confirmou a alteração de modelo esperada; nenhuma
  migration foi criada ou editada no Lote 3B.
- O padrão de mapper de resultados foi aplicado a Nutrition, Packs e Training
  com sete mappers específicos. Clients permaneceu explícito por usar outcomes
  distintos sem repetição do mesmo contrato.
- Notas canónicas:
  `Sessions/2026-08-16-sprint3-phase3-lot3b-blueprints.md` e
  `Sessions/2026-08-17-sprint3-phase3-lot3b-completion.md`.
