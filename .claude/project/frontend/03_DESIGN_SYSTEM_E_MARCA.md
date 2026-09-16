# Design System e Marca

*2026-09-15 — proposta base; tokens finais validados na Fase 6C com os assets do Codex.*
*2026-09-16 — alinhado com o layout aprovado no Claude Design (ver
[layout/06_ESTADOS_E_TOKENS.md](layout/06_ESTADOS_E_TOKENS.md)); os tokens de cor do
design coincidem com a tabela da §4.*

## 1. Direção visual

**"Futurista sóbrio" para desporto de performance.** Moderno e apelativo sem sacrificar a
leitura rápida que um trainer precisa entre sessões.

- Superfícies escuras profundas com camadas subtis (glass leve só em overlays e topbar).
- Azul da marca como único acento forte: ações primárias, foco, dados em destaque.
- Dashboards em **bento grid**: cartões de tamanhos diferentes por importância.
- Tipografia de display itálica e pesada, a ecoar o logo; UI neutra e legível.
- Micro-interações rápidas (150–250 ms), transições de página discretas, nada decorativo
  que atrase uma tarefa.
- Referências de qualidade: Linear, Vercel dashboard, Whoop, Strava (dados de performance).

## 2. Logo e cores extraídas

Logo: "PT" monograma (P branco + T azul) e "PT MANAGER" em itálico bold condensado, sobre
preto. Cores medidas no ficheiro enviado:

| Cor | Hex | Uso |
|---|---|---|
| Azul marca | `#00A3E9` | Acento primário (dark mode), gráficos, foco |
| Branco marca | `#F7F8FA` | Texto principal em dark, fundos em light |
| Preto marca | `#000103` | Referência do fundo; em UI usar neutros ligeiramente elevados |

Assets finais (SVG do logo, `favicon.svg`, `favicon.ico`, `apple-touch-icon.png`) vêm do
Codex e ficam em `frontend/public/`. Versões necessárias: horizontal completa, só
monograma "PT" (sidebar recolhida e favicon), e variante para fundo claro.

## 3. Contraste — regra crítica

Rácios WCAG calculados:

| Combinação | Rácio | Resultado |
|---|---:|---|
| `#00A3E9` sobre `#0A0F16` (fundo dark) | 6.79 | ✅ texto e gráficos |
| **Texto branco sobre `#00A3E9`** | **2.83** | ❌ **falha AA** |
| Texto `#03131C` sobre `#00A3E9` | 6.66 | ✅ botão primário em dark |
| `#00A3E9` sobre branco | 2.83 | ❌ não usar como texto em light |
| `#0077B6` sobre branco | 4.87 | ✅ primário em light (texto branco em cima também 4.87) |
| `#F7F8FA` sobre `#0A0F16` | 18.08 | ✅ |
| `#94A3B8` sobre `#0A0F16` | 7.49 | ✅ texto secundário dark |
| `#475569` sobre branco | 7.58 | ✅ texto secundário light |

**Consequência:** em dark mode o botão primário é azul `#00A3E9` com texto quase preto; em
light mode o primário é `#0077B6` com texto branco. O azul claro da marca nunca é usado
como cor de texto sobre fundo claro.

## 4. Tokens (proposta)

Definidos em `src/shared/styles/globals.css` com o padrão shadcn (`:root` + `.dark`,
expostos via `@theme inline`). Valores em hex por legibilidade; a 6C pode converter para
`oklch` mantendo os rácios acima.

| Token | Light | Dark |
|---|---|---|
| `--background` | `#F7F8FA` | `#05080C` |
| `--foreground` | `#0B1220` | `#F7F8FA` |
| `--card` | `#FFFFFF` | `#0A0F16` |
| `--card-foreground` | `#0B1220` | `#F7F8FA` |
| `--popover` | `#FFFFFF` | `#0D131C` |
| `--muted` | `#EEF2F6` | `#111823` |
| `--muted-foreground` | `#475569` | `#94A3B8` |
| `--border` | `#E2E8F0` | `#1A2230` |
| `--input` | `#E2E8F0` | `#1A2230` |
| `--primary` | `#0077B6` | `#00A3E9` |
| `--primary-foreground` | `#FFFFFF` | `#03131C` |
| `--accent` | `#E6F6FD` | `#0B2231` |
| `--accent-foreground` | `#005A8A` | `#7DD3FC` |
| `--ring` | `#0077B6` | `#00A3E9` |
| `--success` | `#15803D` | `#22C55E` |
| `--warning` | `#B45309` | `#F59E0B` |
| `--destructive` | `#DC2626` | `#EF4444` |
| `--info` | `#0077B6` | `#38BDF8` |
| `--radius` | `0.75rem` | `0.75rem` |

Cada par cor/foreground novo é validado com cálculo de contraste na 6C (script no repo).

Efeitos:

- **Glow de foco/destaque (dark):** `box-shadow: 0 0 0 1px var(--primary), 0 0 24px -6px color-mix(in oklab, var(--primary) 45%, transparent)`. Só em elementos ativos ou KPI principal.
- **Glass (topbar, command menu, sheets):** fundo `color-mix(in oklab, var(--card) 72%, transparent)` + `backdrop-blur-md` + borda `--border`. Nunca em tabelas ou formulários longos.
- **Gradiente de marca:** apenas em hero de dashboard e empty states, de `--primary` para transparente, opacidade baixa.

## 5. Tipografia (adoptada do layout; validar legibilidade na 6C)

| Papel | Fonte | Uso no layout |
|---|---|---|
| Display (títulos, KPIs, marca) | **Saira Condensed** 900 itálico | KPI 56, título de página 36–40, secção/sheet 22 |
| UI e texto | **Geist** 400/600 | Corpo 14 (UI), corpo forte 16 |
| Mono e números | **Geist Mono**, `tabular-nums` | 12 — ids, metadados, kcal, kg, correlation id |

Self-host com `@fontsource` ou ficheiros locais (`font-display: swap`), sem pedidos a CDNs
de terceiros.

Escala: 12 · 14 · 16 (base) · 22 · 36 · 40 · 56. Títulos display em maiúsculas só em
títulos de página e de sheet; nunca em texto corrido.

## 6. Componentes-chave

| Componente | Base shadcn | Comportamento |
|---|---|---|
| **AppShell** | `sidebar` | Sidebar 248 px / 72 px recolhida (monograma "PT", tooltips à direita), `Sheet` 312 px em mobile |
| **Topbar** | — | 56 px, glass; breadcrumbs, botão ⌘K / Ctrl K, toggle de tema, dropdown de perfil (perfil, tema, terminar sessão) |
| **Breadcrumbs** | `breadcrumb` | Gerados a partir das rotas; último nível não clicável |
| **CommandMenu** | `command` + `dialog` | ⌘K / Ctrl+K: navegação, pesquisa por recurso (clientes, planos), ações rápidas por role. Sem atalhos globais com ⇧ (colidem com o browser: ⌘⇧T, ⌘⇧A) |
| **PageHeader** | — | Título (display), descrição, ações primárias à direita |
| **Tabs** | `tabs` | Secções do detalhe do cliente (Resumo, Treino, Nutrição, Suplementos, Check-ins, Sessões) sincronizadas com o URL |
| **DataTable** | `table` + TanStack Table | Paginação servidor, pesquisa com debounce, filtros por `activity`, ações por linha |
| **Combobox** | `popover` + `command` | Ver abaixo |
| **StatCard / BentoGrid** | `card` | KPIs com tendência e ação associada |
| **StatusBadge** | `badge` | Cor + ícone + texto |
| **ConfirmDialog** | `alert-dialog` | Arquivar, cancelar sessão, apagar media |
| **Sheet / Drawer** | `sheet`, `drawer` | Criar/editar rápido sem sair da lista; `drawer` em mobile |
| **EmptyState / Skeleton** | `skeleton` | Obrigatórios em todas as listas |
| **ThemeToggle** | `dropdown-menu` | Claro, escuro, sistema |

### Combobox (escolha de músculo, alimento, exercício, suplemento)

| Caso | Fonte de dados | Modo |
|---|---|---|
| Músculos / grupos musculares | Lista fixa (validada no backend a partir da 6A; decisão final no blueprint) | Multi-seleção com chips, filtro local |
| Alimentos | `GET /foods?search=` + `/global-foods` conforme role | Pesquisa no servidor com debounce 300 ms, React Query, "a carregar" e "sem resultados" |
| Exercícios | `GET /exercises?search=` | Igual a alimentos; mostra badge "global" vs "privado" |
| Suplementos | `GET /supplements?search=` | Igual |

Teclado completo (setas, Enter, Escape), `aria-expanded`, item selecionado anunciado.

## 7. Movimento

- Durações: 150 ms (hover/focus), 200 ms (popover, tabs), 250 ms (sheet, página).
- Easing: `cubic-bezier(0.2, 0.8, 0.2, 1)`.
- Só `transform` e `opacity`. Tudo desativado com `prefers-reduced-motion: reduce`.
- Contagem animada em KPIs apenas no primeiro render.

## 8. Marca do trainer no portal do cliente

O backend já tem `GET /portal/branding` e `PATCH /trainer-settings/branding` (cores e logo
do trainer, com `POST /trainer-settings/branding/reset-colors`). O portal do cliente aplica
a cor do trainer a `--primary` em runtime **só se** passar no contraste mínimo; caso
contrário usa a marca PT Manager. Logo do trainer na sidebar/topbar do portal, com
fallback para o logo PT Manager quando `logo_url` é `null`.

## 9. Layouts por role (ordem de construção)

1. **Admin (6D):** denso e utilitário — tabelas de catálogo global, fila de moderação.
2. **Trainer (6E):** dashboard bento com alertas acionáveis (check-ins por rever, packs a
   acabar, planos a expirar, sessões de hoje); detalhe do cliente com tabs.
3. **Cliente (6F):** mobile-first, navegação inferior, "o meu treino de hoje" em destaque,
   marca do trainer.
4. **Auth (6G):** ecrãs com o logo em destaque e fundo com gradiente de marca subtil.
