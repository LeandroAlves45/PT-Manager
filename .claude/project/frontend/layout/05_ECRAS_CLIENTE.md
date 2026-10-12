# Ecrãs do cliente (portal)

*Artboards 02 (portal), 07 e 07-light · Backend 6A/6B · Frontend 6F · 2026-09-16*

## 1. Home do portal — artboard 02 (painel direito)

**Referência visual:** ![02 Portal](<assets/appshell mobile and admin.png>) — metade superior, telemóvel da direita.

Cabeçalho com logo e nome do negócio do trainer ("Salgado Performance · marca do
treinador") a partir de `GET /portal/branding` (`app_name`, `logo_url`).

| Cartão | Texto do mockup | Origem | Estado |
|---|---|---|---|
| O meu treino de hoje | "Empurrar A · 5 exercícios" | Treino de hoje (6B) | ✅ `GET /portal/home` (cartão) e `GET /portal/my-workout/today` existem desde a 6B (nota 2026-10-04; antes só havia `my-plan`). "Empurrar A" não existe no modelo (o dia só tem `WeekNumber`, `DayOfWeek` e `Notes` opcional): título "Semana 3 · Terça", notas do dia como subtítulo se existirem |
| Plano alimentar | "2 340 kcal · 4 refeições" | `GET /portal/my-nutrition` (`target_kcal`, `meals[]`) | ✅ |
| Suplementos | "3 tomas · 1 em atraso" | `GET /portal/my-supplements/intakes/today` (6A) | ⚠️ ajuste: "X de Y tomadas" / "1 por tomar" — sem "em atraso" (decisão 6A D3) |
| Check-in semanal | "responder até 21/09/2026" | `GET /portal/my-check-ins/due` | ⚠️ `due` só devolve check-ins com data de hoje; "responder até" depende da semântica de `TargetDate` (6B) |

## 2. O meu treino de hoje — artboard 07

**Referência visual:**
![07 Treino de hoje dark](<assets/client and trainer screenshots.png>) — metade inferior (07 dark, com notas de white-label);
![07 Treino de hoje light](<assets/light mode.png>) — fundo da imagem (07 light, só o topo: título, chip e progresso).

### Mantém-se

- Mobile-first 390 px; título display "O MEU TREINO DE HOJE", data, chip do treino,
  "5 exercícios · ~55 min".
- Progresso no topo com valor numérico além da barra ("2 de 5 · 40 %").
- Cartão de exercício 12 px, colapsável; séries em linhas 44 px com checkbox 24 px e ação
  "registar".
- Botão fixo "Concluir treino".
- **White-label:** `--brand` e `--brand-fg` vêm da marca do trainer; tudo o resto herda os
  neutros PT Manager. O azul PT `#00A3E9` só aparece em avisos de sistema.
- Contraste validado: se a cor do trainer falhar 4.5:1 com branco, o texto passa a escuro
  (mockup: `#E8642A` dark / `#C24616` light). Logo do trainer a 32 px; fallback monograma
  em caixa com raio 8 px.

### Ajustes obrigatórios

| No mockup | Ajuste |
|---|---|
| Registar série e "Concluir treino" | 6A: `POST/PATCH/DELETE /portal/exercise-set-logs`, `POST /portal/workout-completions`; ids em `my-plan` (`exercises[].id`, `days[].id`); só hoje; desmarcar bloqueado depois de concluir |
| "Série 1 · 16 kg × 8" | Prescrição de `ExerciseSet` (`PlannedWeightKg`, `PlannedReps`); ao registar, o cliente pode alterar kg e reps realizados |
| "4 × 8 · 90s descanso" | "4 séries · 8 reps · 90–120 s" (descanso é intervalo) |
| "Empurrar A" | Não existe nome de treino A/B; ver §1 |
| "~55 min" | Não existe duração estimada no backend: remover ou calcular no blueprint 6B e documentar a fórmula |
| Cor `primary_color` do trainer | `GET /portal/branding` devolve `primary_color` e `body_color` (opcionais); sem cor → marca PT Manager |

### Atualização 2026-10-04 (plano 6F)

Decisões e contratos atuais em `docs/blueprints/frontend-files/sprint_6/sprint_6F/00_plano_fase_6F_completa.md`:
- Início com item próprio na barra (5 itens);
- "~55 min" removido;
- vídeo do exercício na 6F-2 (o backend passa a expor `exercise_id`/`has_ready_video`);
- a cor principal tem o tom ajustado ao AA por tema, e `body_color` pinta só o cabeçalho.

### Estados a desenhar na 6F

- Sem plano activo: "O teu treinador ainda não atribuiu um plano."
- Dia de descanso (sem treino hoje): mostrar próximo treino.
- Treino concluído: resumo (séries registadas ÷ planeadas).
- Erro ao registar série: manter o valor na linha e permitir repetir; nunca perder input.

## 3. Outros ecrãs do portal (sem artboard)

| Ecrã | Endpoints | Notas |
|---|---|---|
| Nutrição | `GET /portal/my-nutrition` | Refeições com alimentos e quantidades em g; totais alvo e actuais |
| Suplementos | `GET /portal/my-supplements`, `/{assignmentId}` + tomas (6A) | `ServingSize` e `Timing` como texto do trainer |
| Check-ins | `GET /portal/my-check-ins/due`, `POST /portal/check-ins/{checkInId}/respond` | Formulário com peso, medidas e feedback |
| Perfil | `GET/PATCH /portal/my-profile`, `PUT/DELETE /portal/my-profile/avatar` | Avatar moderado de forma síncrona: recusa chega como erro de validação no upload (sem estado pendente) |

### Atualização 2026-10-11 (6F-3, nutrição e suplementos)

Decisões U16–U19 em `docs/blueprints/frontend-files/sprint_6/sprint_6F/sprint_6F3/01_6F3_indice_decisoes.md`:
- os "totais actuais" da nutrição são a soma dos alimentos prescritos, não consumo: o ecrã
  mostra "Objetivo" ao lado de "Este plano dá";
- cada alimento mostra gramas e kcal; os macros ficam no total da refeição;
- os suplementos ficam num só ecrã (tomas de hoje com as notas do trainer), sem paginação;
- marcar a toma espera pelo servidor; `GET /portal/my-supplements/{assignmentId}` não é usado.

### Atualização 2026-10-11 (6F-4, check-ins e perfil)

Decisões U20–U26 em `docs/blueprints/frontend-files/sprint_6/sprint_6F/sprint_6F4/01_6F4_indice_decisoes.md`:
- o check-in responde-se em três passos (Peso → Medidas → Como correu), num só formulário; só
  o peso é obrigatório, e a adesão é uma caixa de número de 0 a 100;
- `due` e `next` respondem 404 `resource_not_found` quando não há: é estado vazio, não erro;
- depois de enviar aparece a confirmação e o próximo check-in; não há histórico para o cliente;
- o rascunho vive só em memória e sair com dados escritos pede confirmação;
- o perfil mostra fotografia, dados da ficha (só de leitura) e contactos; o `PATCH` substitui
  os quatro campos;
- a fotografia não tem pré-visualização local nem estado "em análise"; em desenvolvimento o
  envio responde 503 (Cloudinary e Vision desligados);
- o menu do topo mostra a fotografia (ou as iniciais) e o nome do cliente;
- uma releitura falhada com dados em cache mostra um aviso e mantém os dados, em todas as
  páginas do portal;
- o conteúdo do portal fica numa coluna de 576 px centrada em ecrãs largos.
