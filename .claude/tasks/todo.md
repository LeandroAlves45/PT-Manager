# TODO: ajustes pré-8D (2026-11-03)

Branch `feature/pre-8d-ajustes`. Plano: `C:\Users\Leandro Alves\.claude\plans\fluffy-leaping-pretzel.md`.
Guia de aceitação: `.claude/SPEC.md` (local, apagado no fim).

## Pendente de fases anteriores (Sprint 8+9)
- [ ] 8C: Leandro executa o runbook; Claude valida (CIDR da Render medido no passo 4)
- [ ] Leandro: rever `catalog-data/*.csv`, importar em dev e em produção
- [ ] 8D-4 Capacitor, 8D-5 guias das lojas, 8D-6 submissão (`docs/blueprints/sprint_8/8D/01_o_que_falta.md`)

## Este trabalho
- [x] Branch, SPEC.md, todo.md
- [x] Macros por plano: domínio, configuração, migration `AddMealPlanShowMacrosToClient`, contratos, portal com macros `null`
- [x] Vídeo por link: validador partilhado com allowlist, `video_url` no portal
- [x] Frontend: Nutrição sem barras, chips por flag, interruptor no editor, campo de link, botão "Ver no YouTube"
- [x] Avisos de build/dev + responsividade
- [x] Site: capturas reais no lugar do painel ilustrativo
- [x] Suites completas, mutações, code-review-leandro
- [x] `ci.yml` com `feature/**`, push do branch, CI verde
- [x] `docs/blueprints/sprint_8/8D/02_ajustes_pre_8D.md` (com passos do Neon)
- [x] Quality gates (7A doc 09 e `backlogs/QualityGates.md`)
- [x] Memória: ACTIVE, MEMORY, NEST, sessão, GOTCHAS, lessons
- [x] SPEC "Finalizado" e apagado

## Review
- Feito e verde: CI run `41652117839` em `45e0386` (5 jobs). Backend 3653 + 1 skip, frontend 1145
  (cobertura 90,75/89,18/88,36/82,06), site 121, mutações 3 backend + 7 frontend.
- Extra pedido a meio: saudação do painel com o nome ("Boas-vindas" neutro).
- Pendente do Leandro: Neon (consulta e teste da migration), merge e deploy pela ordem
  migration → Render → Vercel; aprovar as mudanças propostas em `docs_about_app` (RN-014, RN-012).
