# Todo — Auditoria de autorização (Application): Clients, Packs, Training, Nutrition

Plano aprovado: `C:\Users\Leandro Alves\.claude\plans\claude-desejo-que-atues-parallel-glacier.md` (2026-08-20).
Entrega documental em `docs/backend-files/sprint_3/fase_3/lote_3D/auditoria_autorizacao_application/`.
Sem tocar `backend/src`, `backend/tests` ou migrations (decisão do utilizador).

## Investigação (4 agentes)

- [x] Estado real de Clients (Domain/Application/Infrastructure/testes)
- [x] Estado real de Packs (PackType + ClientSessionPack)
- [x] Padrões comuns já em código real (SearchTerm, ActorAuthorization, LikeSearchPattern, translator, interceptor)
- [x] Auditoria de correção da migração ActorAuthorization (ordem, erros, redundância, cobertura, `dotnet test` real) em Clients/Packs/Training/Nutrition

## Documentos

- [x] 00_indice_e_achados.md — achados por gravidade, decisões do utilizador, estado do git
- [x] 01_correcao_test_doubles_userid.md — 6 ficheiros, `UserId` default válido + parâmetro `role`
- [x] 02_correcao_erro_mealplan_trainer_only.md — `NutritionErrors.MealPlanTrainerOnly` + 6 handlers de MealPlans
- [x] 03_testes_autorizacao_role_errada.md — 4 testes novos "role errada → Forbidden" (Clients/Packs/Training/Nutrition)
- [x] 04_testes_domain_client_em_falta.md — 10 testes novos de `Client` (Deactivate/Reactivate/SoftDelete/SetAvatar)
- [x] 05_gate_e_autoavaliacao.md — veredito por ponto, contagem estrutural, pendências

## Verificação

- [x] Contagem: 18 `## Ficheiro` == 18 blocos ```csharp == 18 `Caminho:` (6+7+4+1), confirmada por `grep -c`
- [x] 15 ficheiros reais distintos identificados; 3 reaparecem no doc 03 como delta sobre ficheiro já mostrado no doc 01 (assinalado, não acidental)
- [x] Zero TODO/pseudocódigo/`NotImplementedException` confirmado por grep
- [x] Códigos de erro usados nas novas asserções (`clients_trainer_only`, `packs_trainer_only`, `training_trainer_only`, `food_trainer_only`) confirmados por leitura direta do código real
- [x] git status: `docs/` no `.gitignore` — nada versionado por esta tarefa

## Memória

- [x] Nota de sessão: `Sessions/2026-08-20-auditoria-autorizacao-clients-packs-training-nutrition.md`
- [x] MEMORY.md atualizado com secção nova
- [x] Regra 15 em `Patterns/blueprints_codigo_real_por_ficheiro.md`

## Aplicação real ao código (2026-08-21)

- [x] doc 01 aplicado — 6 test doubles corrigidos (`UserId` default + parâmetro `role`)
- [x] doc 02 aplicado — `NutritionErrors.MealPlanTrainerOnly` + 6 handlers de `MealPlans`
- [x] doc 03 aplicado — 4 testes novos "role errada → Forbidden"
- [x] doc 04 aplicado — 10 testes novos de `Client` Domain
- [x] `dotnet test` Clients+Packs+Training+Nutrition: 125/125 passam, 0 falhas
- [x] `dotnet test` Domain `ClientTests`: 21/21 passam, 0 falhas (via `git stash`
      temporário dos 3 ficheiros do Lote 3E que bloqueiam o build do projeto,
      revertido de imediato a seguir)
- [x] Confirmado por grep: `Exercises` (6 handlers) e `TrainingPlans`
      (7 handlers) já usavam `ActorAuthorization` com separação de erro
      correta — serviram de modelo para a correção de `MealPlans`
- [x] `/ponytail-ptmanager`: nenhuma abstração nova, alterações mínimas
- [x] `/code-review-leandro`: sem críticos nem avisos

## Pendente (fora do âmbito desta tarefa, por decisão do utilizador)

- [ ] Commit dos ficheiros (o utilizador fará essa parte)
- [ ] Migration de PackType/ClientSessionPack — diferida para o Lote 3F
- [ ] Ordem validar→autorizar nos 43 handlers — sinalizada, não alterada
- [x] `ExerciseSetLogs/*` corrigido (2026-08-21): os 3 handlers passaram a
      usar `ActorAuthorization.RequireTrainer` com novo
      `TrainingErrors.ExerciseSetLogTrainerOnly` (`exercise_set_log_trainer_only`),
      decisão baseada em evidência (padrão CheckIns: self-service do cliente
      é sempre `RequireClient` explícito por nome de handler; `Record`/
      `Correct`/`List` não têm esse padrão nem endpoint Api associado — não
      é suposição). 6 testes novos em
      `ExerciseSetLogHandlersTests.cs` (sucesso + role errada por handler).
      316/316 testes de `Application.UnitTests` passam (suite completa).

## Review

**O que foi feito**: auditoria de correção (não de existência) da migração
para `ActorAuthorization`/`SearchTerm` em Clients, Packs, Training e
Nutrition — 4 áreas já totalmente implementadas em código real antes desta
tarefa. 3 agentes de exploração + 1 agente de auditoria com execução real de
`dotnet test` encontraram dois bugs reais já presentes no código não
commitado: 72/134 testes a falhar por `UserId` nunca definido nos test
doubles (produção não afetada), e `NutritionErrors.TrainerOnly` reutilizado
indevidamente por `MealPlans`. Confirmaram também que a cobertura do helper,
a ausência de checks residuais e o tratamento de `Result<T>` estão corretos
nos 43 handlers auditados.

**Decisão chave**: por pedido explícito do utilizador, as correções ficam
só documentadas (prontas a copiar) — nada foi alterado em `backend/src` ou
`backend/tests`. O commit dos 76 ficheiros pendentes também fica para o
utilizador decidir.

**Atualização 2026-08-21**: o utilizador pediu para aplicar as correções
documentadas ao código real. Os 15 ficheiros dos docs 01-04 foram aplicados,
verificados por build limpo e execução real de testes (125/125 Application,
21/21 Domain `Client`), e `Exercises`/`TrainingPlans` confirmados já corretos
por grep direto. Achado novo fora do âmbito: `ExerciseSetLogs/*` não valida
`Role` (usa `GetRequiredTrainerId`, não `ActorAuthorization`) — sinalizado,
não corrigido, por não fazer parte do pedido original. Nada foi commitado.

**Lição principal**: extrair um helper partilhado que endurece uma
validação (aqui, `UserId` obrigatório) exige verificar por execução real —
não só por leitura — se os test doubles de todas as features consumidoras
continuam válidos. Registada como regra 15 em
`blueprints_codigo_real_por_ficheiro.md`.
