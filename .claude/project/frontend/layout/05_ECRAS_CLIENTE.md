# Ecrãs do cliente (portal)

*Artboards 02 (portal), 07 e 07-light · Backend 6A/6B · Frontend 6F · 2026-09-16*

## 1. Home do portal — artboard 02 (painel direito)

**Referência visual:** ![02 Portal](<assets/appshell mobile and admin.png>) — metade superior, telemóvel da direita.

Cabeçalho com logo e nome do negócio do trainer ("Salgado Performance · marca do
treinador") a partir de `GET /portal/branding` (`app_name`, `logo_url`).

| Cartão | Texto do mockup | Origem | Estado |
|---|---|---|---|
| O meu treino de hoje | "Empurrar A · 5 exercícios" | Treino de hoje (6B) | ⚠️ hoje só `GET /portal/my-plan` sem noção de "hoje". "Empurrar A" não existe no modelo (o dia só tem `WeekNumber`, `DayOfWeek` e `Notes` opcional): título "Semana 3 · Terça", notas do dia como subtítulo se existirem |
| Plano alimentar | "2 340 kcal · 4 refeições" | `GET /portal/my-nutrition` (`target_kcal`, `meals[]`) | ✅ |
| Suplementos | "3 tomas · 1 em atraso" | `GET /portal/my-supplements` + tomas (6A) | ❌ até 6A; "em atraso" depende da decisão sobre `Timing` |
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
| Registar série e "Concluir treino" | Só depois da 6A (endpoints do portal, autorização exclusiva do cliente, plano activo) |
| "Série 1 · 16 kg × 8" | Prescrição de `ExerciseSet` (`PlannedWeightKg`, `PlannedReps`); ao registar, o cliente pode alterar kg e reps realizados |
| "4 × 8 · 90s descanso" | "4 séries · 8 reps · 90–120 s" (descanso é intervalo) |
| "Empurrar A" | Não existe nome de treino A/B; ver §1 |
| "~55 min" | Não existe duração estimada no backend: remover ou calcular no blueprint 6B e documentar a fórmula |
| Cor `primary_color` do trainer | `GET /portal/branding` devolve `primary_color` e `body_color` (opcionais); sem cor → marca PT Manager |

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
