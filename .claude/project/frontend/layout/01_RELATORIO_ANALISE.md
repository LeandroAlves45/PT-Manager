# Relatório de análise do layout — Claude Design × v0 × backend real

*2026-09-16. Análise dos 13 artboards do Claude Design (modo Present), do app-shell do v0 e
verificação de cada elemento contra o código em `backend/src/` (só leitura).*

## 1. Resumo executivo

- O **Claude Design** é a base: coerente com `03_DESIGN_SYSTEM_E_MARCA.md` (os tokens de
  cor coincidem), denso onde deve (admin), orientado à ação onde deve (dashboard) e
  mobile-first no portal.
- O **v0** fica como comparação. Usa Next.js (o projeto é Vite), acentos violeta/laranja/
  ciano fora da marca e Arial. Único elemento adoptado: **dropdown de perfil** na topbar.
- O backend tem 142 endpoints v1, todos por recurso. O mockup pressupõe capacidades que
  **não existem** (dashboard agregado, RPE, séries e tomas pelo cliente, fila de moderação)
  e alguns dados **que contradizem o código** (PRO com limite de 25, kcal editável).
- Decisão do utilizador: **backend-first**. Sprint 6 abre com 6A (escrita/schema) e 6B
  (leituras agregadas); frontend passa a 6C–6G.

## 2. Decisões do utilizador (2026-09-16)

| Tema | Decisão |
|---|---|
| Dashboard do trainer | Endpoint agregado no backend **antes** do frontend (6B) |
| RPE nas séries | **Entra** (6A) |
| Porção padrão do alimento | **Entra** (6A) |
| Registo de séries e concluir treino pelo cliente | **Entra** (6A; DEF-PORTAL-001 antecipado do 10C) |
| Registo de tomas de suplementos | **Entra** (6A) |
| Vendas de packs estimadas no dashboard | **Entra** (6B) |
| Fila de moderação do superuser | **Entra** (6B) |
| Adesão ao treino calculada | **Entra** (6B) |
| Check-in "revisto" | **Entra** (6A) |
| Restantes PARCIAIS | Passam a SUPORTADOS em 6A/6B |
| Categoria de alimento | **Excluído** |
| Notas de moderação / fonte no alimento global | **Excluído** (sem benefício) |
| Importação CSV de alimentos | **Futuro** (DEF-PROD-004) |
| Notificações in-app | **Futuro** (DEF-PROD-005) |
| Endpoint de pesquisa transversal | **Futuro** (DEF-PROD-006); ⌘K usa `search` por recurso |
| Do v0 | Só o dropdown de perfil |
| Referência visual | PNG em `assets/` (mapeamento no [README](README.md)); sem links do Claude Design nem PDF |

## 3. Matriz elemento do mockup × backend

Caminhos relativos a `backend/src/`. Veredicto à data de 2026-09-16, antes de 6A/6B.

| # | Elemento (artboard) | Veredicto | Evidência | Destino |
|---|---|---|---|---|
| 1 | Categoria do alimento (03) | ❌ | `Domain/Entities/Nutrition/Food.cs` não tem categoria | Excluído |
| 2 | Porção padrão "150 g" (03) | ❌ | `Food.cs`: macros por 100 g, sem porção; plano usa `QuantityInGrams` | 6A |
| 3 | Kcal editável (03) | ⚠️ contradiz | `Food.cs`: `Kcal` é coluna gerada `protein*4 + carbs*4 + fats*9`, só leitura | Ajuste de UI |
| 4 | Fibra | ✅ | `Food.cs`: `Fiber` opcional | — |
| 5 | Switch "Visível no catálogo" (03) | ✅ outra forma | `POST /global-foods/{id}/archive` e `/reactivate`; filtro `activity` | Ajuste de UI |
| 6 | Notas de moderação, fonte "INSA 2019" (03) | ❌ | Só `PlatformEnforcementStatus/Reason/EnforcedAt` em `Food.cs` | Excluído |
| 7 | "Importar CSV" (09) | ❌ | Sem rota nem código CSV | Futuro |
| 8 | Paginação "25 por página" (03) | ✅ | `Api/Contracts/Common/PageParameters.cs:12` (omissão 50); `Application/Validation/PaginationValidationRules.cs:31` (1–100) | Usar 25 explícito |
| 9 | Grupos musculares multi-seleção (06) | ⚠️ parcial | `Domain/Entities/Training/Exercise.cs:15` `MuscleGroups` string livre ≤ 500 | 6A (lista fixa validada) |
| 10 | Dashboard agregado (04) | ❌ | Nenhuma rota de dashboard | 6B |
| 11 | Check-ins por rever (04) | ⚠️ parcial | `Infrastructure/Persistence/Assessments/CheckInQueries.cs:103-121`: estados `Scheduled/Answered/Missed/Cancelled`, sem "revisto" | 6A + 6B |
| 12 | Packs a terminar (04) | ⚠️ derivável | `Domain/Entities/Billing/ClientSessionPack.cs:17,21`: `SessionsRemaining`, `ExpectedEndDate`; sem filtro | 6B |
| 13 | Planos a expirar (04) | ⚠️ parcial | Datas de fim nos summaries; sem filtro por data em `GET /training-plans`, `/meal-plans` | 6B |
| 14 | Sessões de hoje + "Registar presença" (04) | ✅ | `GET /sessions?starts_from&starts_before`; `POST /sessions/{id}/complete`, `/no-show` | — |
| 15 | Clientes sem plano activo (04) | ⚠️ parcial | Só cruzando `GET /clients` com `GET /training-plans` | 6B |
| 16 | "Facturação 2 480 €" (04) | ❌ | Sem pagamentos de clientes; `ClientSessionPack.cs:18,20` `PriceCents`, `PurchaseDate` | 6B como "vendas de packs (estimado)" |
| 17 | "Plano PRO · 18 de 25 clientes" (01) | ⚠️ contradiz | `Domain/ValueObjects/SubscriptionTier.cs:17-19`: FREE 5, STARTER 25, **PRO ilimitado**; `GET /billing/subscription` devolve `client_limit`, `current_client_count` | Ajuste de UI |
| 18 | Idade, "cliente desde", objetivo (05) | ✅ | `Api/Contracts/Clients/ClientContracts.cs:63-79`: `BirthDate`, `Objective`, `CreatedAt`, `UsablePacks` | — |
| 19 | Peso e altura (05) | ⚠️ parcial | Avaliação inicial (`GET /clients/{id}/initial-assessment`); peso actual via check-ins | 6B (resumo) |
| 20 | "Adesão ao treino 86% · 24 de 28 sessões" (05) | ❌ | Só `TrainingAdherenceScore` auto-declarado em `Domain/Entities/Assessments/CheckIn.cs:21` | 6B (séries registadas ÷ planeadas) |
| 21 | "Kcal alvo 2 200 · macros" (05) | ✅ | Summary/detalhe do plano alimentar activo | 6B (resumo) |
| 22 | "Pack renova 30/09" (05) | ⚠️ contradiz | Não há renovação; só `ExpectedEndDate` | Ajuste de texto |
| 23 | Check-ins com peso e texto (05) | ✅ | `CheckIn.cs:16-20`: `WeightKg`, `Notes`, `Feedback`, medidas | — |
| 24 | Plano "A/B · 4×6 · RPE 8 · 62,5 kg" (05) | ⚠️ parcial | `Domain/Entities/Training/TrainingPlanDay.cs:12,16` semana × dia; `ExerciseSet.cs:12-16` série a série, reps, kg, descanso min–máx; **sem RPE** | 6A (RPE) + ajuste de UI |
| 25 | Treino de hoje (07) | ⚠️ parcial | `GET /portal/my-plan` com dias; sem noção de "hoje" | 6B |
| 26 | Cliente regista série / "Concluir treino" (07) | ❌ | `Api/Controllers/ExerciseSetLogsController.cs:15` `[Authorize(ApiPolicyNames.Trainer)]` | 6A |
| 27 | "Suplementos · 3 tomas · 1 em atraso" (02) | ❌ | `Domain/Entities/Supplements/ClientSupplementAssignment.cs:11-13`: `ServingSize`, `Timing` texto livre | 6A |
| 28 | "Check-in semanal · responder até" (02) | ⚠️ parcial | `CheckInQueries.cs:83`: `due` só com `CheckInDate == localToday`; `TargetDate` opcional | 6B (semântica) |
| 29 | Plano alimentar "2 340 kcal · 4 refeições" (02) | ✅ | `GET /portal/my-nutrition` | — |
| 30 | Marca do trainer no portal (07) | ✅ | `Api/Contracts/Portal/PortalContracts.cs:10-14`: `AppName`, `LogoUrl`, `PrimaryColor`, `BodyColor` | — |
| 31 | Moderação admin | ⚠️ parcial | Só `POST /admin/content-moderation/{foods,exercises}/{id}/block|unblock`; sem listagem | 6B (fila) |
| 32 | Sino de notificações | ❌ | `Domain/Entities/Notifications/Notification.cs` é fila de email (`RecipientEmail`) | Futuro |
| 33 | Pesquisa ⌘K transversal (08) | ❌ | Só `search` por recurso | Futuro (servidor); UI com pesquisas por recurso |
| 34 | Tipo e local de sessão "PT individual · Estúdio A" (04) | ⚠️ texto livre | `Domain/Entities/Sessions/Session.cs:15-16` `Location`, `SessionType` opcionais | Mostrar só se preenchido |
| 35 | Correlation id no erro (09) | ✅ | `02_CONTRATO_HTTP_FRONTEND.md`: `correlation_id` nos ProblemDetails e header `X-Correlation-ID` | — |

## 4. O que faz sentido manter

1. **AppShell:** sidebar 248/72 px, topbar glass 56 px, item activo com fundo azul 12 % +
   barra 2 px à esquerda, hover 120 ms.
2. **Mobile:** Sheet lateral 312 px com overlay; portal com barra inferior de 4 itens, alvos
   ≥ 44 px, item activo com ícone preenchido + texto + barra superior (nunca só cor).
3. **Admin denso:** linhas 48 px, Sheet de edição 440 px com rodapé fixo, filtro em Tabs,
   ações por linha sempre visíveis (sem hover-only).
4. **Dashboard bento:** cada alerta com número display, contexto e **uma só ação**; glow
   reservado ao KPI principal; severidade por cor + ícone + texto.
5. **Detalhe do cliente:** cabeçalho com avatar e badges, tabs com underline e label
   visível, KPIs em 4 colunas.
6. **Combobox:** lista ≤ 320 px, itens 44 px, badge global/privado com ícone e texto,
   skeleton em vez de spinner, chips removíveis por Backspace, atalhos no rodapé.
7. **Portal white-label:** `--brand` do trainer substitui só o acento; neutros PT Manager;
   fallback de contraste 4.5:1; logo do trainer 32 px com fallback de monograma.
8. **Estados:** vazio (título + frase + ação), skeleton com a métrica real das linhas,
   erro humano com correlation id copiável e `aria-live="assertive"`.
9. **Tokens e tipografia:** Saira Condensed 900 itálico, Geist, Geist Mono tabular.

## 5. O que não faz sentido / ajustes obrigatórios

| # | No mockup | Problema | Ajuste |
|---|---|---|---|
| 1 | Coluna e campo "Categoria" | Excluído pelo utilizador | Remover; colunas: nome, kcal, P, HC, G, fibra, estado |
| 2 | Kcal editável | Coluna gerada no backend | Kcal só leitura, recalculada em tempo real a partir das macros |
| 3 | Switch "Visível no catálogo" no formulário | Arquivar é ação própria com efeito para todos os trainers | Botão "Arquivar/Reativar" fora do formulário, com ConfirmDialog |
| 4 | "Notas de moderação", "INSA 2019", "submetido por PT" | Excluído | Remover |
| 5 | "Plano PRO · 18 de 25" | PRO é ilimitado | `tier` + `current_client_count` / `client_limit`; `null` → "ilimitado", sem barra |
| 6 | "Facturação · 2 480 € · +12 %" | Não há pagamentos de clientes | "Vendas de packs (estimado)" do mês, sem comparação até existir no endpoint 6B |
| 7 | Plano "A/B", "4 × 6" agregado | Modelo real é semana × dia e série a série | Agrupar por semana e dia da semana; mostrar séries individuais; RPE depois da 6A |
| 8 | "Adesão 86 % · 24 de 28 sessões" | Fórmula inexistente e errada (sessões ≠ treino) | Adesão = séries registadas ÷ planeadas (6B), com período explícito |
| 9 | "Pack renova 30/09" | Não há renovação | "Fim previsto 30/09" |
| 10 | "Importar CSV" no vazio | Futuro | Remover; só "+ Novo alimento" |
| 11 | Erro "rascunho local… Reportar" e "ERRO 500" | Sem rascunhos locais nem endpoint de report; título técnico | Manter dados no formulário, "Tentar novamente", copiar correlation id; título humano |
| 12 | Atalhos ⌘⇧S, ⌘⇧T, ⌘⇧A | ⌘⇧T reabre separador e ⌘⇧A pesquisa separadores no browser | Sem atalhos globais com ⇧; ações só dentro da palette; `Ctrl K` em Windows |
| 13 | Sidebar do trainer sem Check-ins nem Biblioteca | Não bate com as rotas de `00_ARQUITETURA_FRONTEND.md` §6 | Navegação revista em [02_APPSHELL_NAVEGACAO.md](02_APPSHELL_NAVEGACAO.md) |
| 14 | "Mensagens · Brevemente" | Chat é futuro; doc 04 exige aprovação por ecrã | Remover |
| 15 | Ícones geométricos | Placeholders | Lucide |
| 16 | "Supino inclinado · 4 × 8 · 90s descanso" | Descanso é intervalo min–máx | "90–120 s" quando min ≠ máx |
| 17 | Sino de notificações implícito na topbar do v0 | Futuro | Sem sino |

## 6. O que acrescentar / melhorar

1. **Dropdown de perfil (v0):** nome e role, "O meu perfil" (trainer: definições; cliente:
   `/portal/my-profile`), tema, **terminar sessão** (`POST /auth/logout`).
2. **Banners de conta:** email por verificar (trainer, TTL 24 h) e subscrição com problema
   de pagamento (estado de `GET /billing/subscription`).
3. **Dashboard vazio de trainer novo:** onboarding com "Convidar primeiro cliente"
   (`POST /auth/invite-client`) em vez de cartões a zero.
4. **Páginas de sistema:** 403 (redireciona para a home do role), 404, sessão expirada.
5. **Fluxo de convite de cliente** e estado "convite pendente" na lista de clientes.
6. **Vídeo de exercício (5D):** estados pendente, a processar, pronto, falhado, e quota de
   20 exercícios com vídeo por trainer.
7. **Avatar do cliente recusado pela moderação:** a moderação é síncrona no upload
   (`ReviewRequired`/`Rejected` → erro de validação em
   `Application/Common/Media/MediaPreparationErrorMapper.cs:31`); não há estado
   "pendente", por isso a UI mostra o erro no próprio upload.
8. **Toasts** (sonner) para sucesso e **ConfirmDialog** para arquivar, cancelar e apagar.
9. **Tabelas do trainer em cards** abaixo de 768 px.
10. **Versões light** de 03, 05 e 06 (o mockup só tem 01, 04 e 07).
11. **Formatos pt-PT:** datas `16/09/2026`, números `1 284 kcal`, `64,8 kg`, moeda `2 480 €`.
12. **Acessibilidade:** foco visível em todos os controlos, `prefers-reduced-motion`,
    tooltips da sidebar recolhida acessíveis por teclado.

## 7. v0 — avaliação

| Elemento v0 | Decisão | Motivo |
|---|---|---|
| Dropdown de perfil | **Adoptado** | Falta no Claude Design; necessário para terminar sessão |
| Saudação com data em eyebrow | Não | O Claude Design já tem ("Terça, 16/09/2026 · Bom treino, Ricardo") |
| Gráfico "Atividade semanal" | Não | Decisão do utilizador |
| Cartão "Desbloqueie mais / Pronto para crescer?" | Não | Genérico; substituído pelo cartão de subscrição com dados reais |
| Paleta violeta/laranja/ciano, Arial | Não | Fora da marca (`#00A3E9`, Saira/Geist) |
| Itens "Progresso", "Faturação", "Ajuda e suporte" | Não | Sem backend ou fora do âmbito |
| Next.js | Não | O projeto usa Vite (decisão de 2026-09-15) |

## 8. Decisões abertas (fechar nos blueprints 6A/6B, sem supor)

1. Grupos musculares: lista fixa validada no backend mantendo a coluna string (sem
   migration de dados) — confirmar valores existentes na base dev.
2. Tomas de suplementos: como definir "em atraso" se `Timing` é texto livre.
3. RPE: escala (1–10, meios pontos?) e opcionalidade por série.
4. Concluir treino: parcial permitido? efeito em sessões e packs (nenhum, por omissão).
5. Adesão: período (ex.: 28 dias) e tratamento de semanas sem plano.
6. Limiares: "pack a terminar" (ex.: ≤ 2 sessões) e "plano a expirar" (ex.: ≤ 7 dias).
7. `TargetDate` do check-in: confirmar se significa "responder até" antes de o mostrar.
8. Dashboard: top-N por bloco e fuso horário (do trainer) para "hoje" e "mês".
