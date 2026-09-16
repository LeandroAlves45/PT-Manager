# Ecrãs do personal trainer

*Artboards 04, 04-light, 05 e 06 · Backend 6A/6B · Frontend 6E · 2026-09-16*

## 1. Painel (dashboard bento) — artboard 04

**Referência visual:**
![04 Dashboard dark](<assets/trainer dash.png>) — metade superior (04 dark);
![04 Dashboard light](<assets/light mode.png>) — meio da imagem (04 light).

### Mantém-se

- Bento de 12 colunas, gap 16 px, cantos 12 px.
- Cabeçalho: data em eyebrow ("Terça, 16/09/2026"), saudação display ("Bom treino,
  Ricardo"), ação primária "+ Novo cliente".
- Cada alerta: número display, contexto e **uma só ação**.
- Glow `0 0 60px -20px rgba(0,163,233,.5)` só no KPI principal (check-ins por rever).
- Severidade por cor + ícone + texto.

### Blocos e origem dos dados

Todos os blocos vêm de **um** endpoint agregado criado na 6B (caminho final no blueprint).

| Bloco | Conteúdo | Ação | Hoje | Depois de 6A/6B |
|---|---|---|---|---|
| Check-ins por rever (KPI principal) | Total, quantos > 48 h, mais antigo | "Rever check-ins" → `/trainer/check-ins?status=por-rever` | ⚠️ só `status=Answered`, sem "revisto" | ✅ respondido e não revisto |
| Packs de sessões a terminar | Top-N clientes, pack, data, restantes | "Renovar packs" → criar pack (`POST /client-session-packs`) | ⚠️ derivável sem filtro | ✅ |
| Planos a expirar | Total nos próximos N dias | "Prolongar" → editar plano | ⚠️ sem filtro por data | ✅ |
| Sessões de hoje | Hora, cliente, tipo e local (se preenchidos) | "Registar presença" → `POST /sessions/{id}/complete` (e "Faltou" → `/no-show`) | ✅ `GET /sessions?starts_from&starts_before` | ✅ |
| Clientes sem plano activo | Top-N com dias sem plano | "Atribuir plano" | ⚠️ só por cruzamento | ✅ |
| **Vendas de packs (estimado)** — mês | Soma de `PriceCents` dos packs comprados no mês | — | ❌ | ✅ |

### Ajustes obrigatórios

| No mockup | Ajuste |
|---|---|
| "Facturação · Setembro · 2 480 € · +12 % vs agosto" e mini-barras | "Vendas de packs (estimado)"; comparação e gráfico só se o endpoint 6B os devolver |
| "PT individual · Estúdio A" | `SessionType` e `Location` são texto livre opcional: omitir quando vazios |
| "Renovar packs" | Não há renovação: a ação cria um pack novo para o cliente |
| Trainer sem clientes | Estado vazio de onboarding: "Convidar primeiro cliente" (`POST /auth/invite-client`) |

## 2. Detalhe do cliente — artboard 05

**Referência visual:** ![05 Detalhe do cliente](<assets/trainer dash.png>) — metade inferior (05).

### Mantém-se

- Cabeçalho com avatar 56 px, nome display, badges de estado e de pack, ações à direita
  (Novo check-in · Avaliação inicial · Editar plano).
- Tabs com underline 2 px `#00A3E9` e label sempre visível, sincronizadas com o URL:
  **Resumo · Treino · Nutrição · Suplementos · Check-ins · Sessões**.
- KPIs em 4 colunas; depois grelha 1.4fr/1fr, gap 16 px.

### Cabeçalho — origem

| Dado | Origem |
|---|---|
| Nome, idade, objetivo, "cliente desde" | `GET /clients/{clientId}` (`birth_date`, `objective`, `created_at`) |
| Peso, altura | Resumo 6B (peso actual via check-ins; altura da avaliação inicial `GET /clients/{clientId}/initial-assessment`) |
| Badge "Ativa" | `is_active` |
| Badge "Pack 10 sessões · 3 restantes" | `usable_packs` do detalhe |

### KPIs (Resumo) — ajustados

| Mockup | Final | Origem |
|---|---|---|
| Peso atual 64,8 kg · −1,4 kg em 8 semanas | Mantém | Resumo 6B (check-ins) |
| Adesão ao treino 86 % · 24 de 28 sessões | **Adesão 86 % · 96 de 112 séries (28 dias)** | Resumo 6B: séries registadas ÷ planeadas |
| Kcal alvo 2 200 · 180 g HC · 140 g P | Mantém | Plano alimentar activo |
| Sessões restantes 3 · pack renova 30/09 | **3 · fim previsto 30/09** | `usable_packs[].expected_end_date` |

### Plano de treino activo — ajustado

O mockup mostra "Força 4×/semana", dias **A/B** e "4 × 6 · RPE 8 · 62,5 kg" agregados.
O modelo real (`TrainingPlanDay`: `WeekNumber` 1–52 × `DayOfWeek`; `ExerciseSet` série a
série) exige:

- Agrupar por **semana → dia da semana** (ex.: "Semana 3 · Segunda").
- Resumo por exercício derivado das séries: "4 séries · 6 reps · 62,5 kg" só quando todas
  as séries são iguais; caso contrário "4 séries · 6–8 reps".
- RPE só depois da 6A (`PlannedRpe`).
- Descanso como intervalo "90–120 s" (`RestSecondsMin`/`RestSecondsMax`).
- Superséries (`ExerciseGroupId`) com marcador visual comum.
- Validade: "válido até 30/09/2026" a partir da data de fim do plano.

### Últimos check-ins — mantém

Data, peso (`WeightKg`, opcional) e texto (`Notes`/feedback). Acrescentar badge "Por rever"
e ação "Marcar como revisto" (6A).

## 3. Adicionar alimento ao plano / grupos musculares — artboard 06

**Referência visual:** ![06 Combobox](<assets/client and trainer screenshots.png>) — metade superior (06).

### Adicionar alimento — mantém

- Combobox (Popover + cmdk): lista ≤ 320 px com scroll, item 44 px.
- Origem em badge com ícone e texto: **Global** / **Privado**.
- "A carregar mais…" com linhas skeleton, nunca spinner isolado.
- Rodapé: `↑↓ navegar · ↵ adicionar · esc fechar` + "Criar alimento privado".

Ajustes: pesquisa em `GET /foods?search=`, que já devolve globais e privados do trainer
com `scope` `global`/`private` (`Infrastructure/Persistence/Nutrition/FoodQueries.cs:79`,
`Api/Contracts/Nutrition/FoodContracts.cs:26`) — o badge usa `scope`; debounce 300 ms,
mínimo 2 caracteres. Linha secundária "165 kcal · 31 g P · 0 g HC / 100 g"
mantém-se (macros por 100 g). "612 kcal / porção" do mockup **não existe**: tudo por 100 g.
Ao adicionar, a quantidade é pré-preenchida com `DefaultServingGrams` (6A) ou fica vazia.

### Grupos musculares — ajustado

- Multi-seleção em chips removíveis por Backspace.
- Lista **fixa** validada no backend a partir da 6A (hoje `MuscleGroups` é string livre ≤
  500). Valores finais decididos no blueprint 6A; o mockup sugere Dorsais, Peito, Deltoide
  posterior, Lombar, Glúteos, Isquiotibiais, Core, Bíceps, Trapézio médio.
- Filtro local, sem pedido ao servidor.

## 4. Ecrãs sem artboard (desenhar na 6E com os mesmos padrões)

| Ecrã | Base visual | Endpoints principais |
|---|---|---|
| Lista de clientes | Tabela densa do artboard 03 | `GET /clients`, `POST /clients`, `POST /auth/invite-client`, archive/reactivate |
| Sessões e packs | Lista do dia + tabela | `/sessions/*`, `/pack-types`, `/client-session-packs/*` |
| Check-ins | Tabela com filtro de estado | `/check-ins/*` + revisto (6A) |
| Editor de plano de treino | Semana × dia, séries | `/training-plans/*`, `PUT /training-plans/{id}/structure` |
| Editor de plano alimentar | Refeições + combobox 06 + preview | `/meal-plans/*`, `/nutrition/preview` |
| Biblioteca | Tabs exercícios/alimentos/suplementos | `/exercises`, `/foods`, `/supplements`, vídeo 5D |
| Definições | Formulários por secção | `/trainer-settings/*` (marca, logo, contactos, fuso horário) |
| Subscrição | Cartão de estado + ações | `GET /billing/subscription`, `POST /billing/checkout`, `/customer-portal` |
