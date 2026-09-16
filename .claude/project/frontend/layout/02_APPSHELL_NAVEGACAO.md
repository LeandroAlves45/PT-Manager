# AppShell, navegação e command palette

*Artboards 01, 01-light, 02 e 08 · Fase 6C (fundações) · 2026-09-16*

**Referência visual** (ler só a imagem necessária):

| Imagem | O que mostra |
|---|---|
| ![01 AppShell desktop dark](<assets/appshell desktop.png>) | 01 dark: sidebar expandida (248 px) e recolhida (72 px) |
| ![01 light](<assets/light mode.png>) | Topo da imagem: 01 light |
| ![02 mobile](<assets/appshell mobile and admin.png>) | Topo da imagem: 02 Sheet + portal |

08 Command palette: **sem screenshot**; usar a especificação da §6.

## 1. Desktop (1440 px)

| Elemento | Especificação do mockup | Nota |
|---|---|---|
| Sidebar expandida | 248 px, gutter 0 | Logo horizontal; botão « para recolher |
| Sidebar recolhida | 72 px | Monograma "PT"; tooltips à direita, delay 200 ms, também por foco de teclado |
| Topbar | 56 px, glass (blur 14 px, `#0A0F16` a 72 %) | Breadcrumbs à esquerda; pesquisa ⌘K, tema, perfil à direita |
| Conteúdo | Fluido, padding 24 px, max-width 1200 px, gap 18 px | — |
| PageHeader | Título display 40/40, descrição 14/21, ações à direita | Uma ação primária no máximo |
| Cantos e bordas | 12 px, bordas `#1A2230` (dark) / `#E3E8EF` (light) | Light troca sombras por bordas, sem glass nos cartões |
| Item activo | Fundo `rgba(0,163,233,.12)` + barra 2 px `#00A3E9` à esquerda + texto `#F7F8FA` | Light: primário `#0077B6` |
| Hover | 120 ms ease-out | Respeitar `prefers-reduced-motion` |
| Rodapé da sidebar | Cartão de subscrição + utilizador | Ver §4 |

## 2. Mobile (390 px)

| Elemento | Especificação |
|---|---|
| Alvos | ≥ 44 px |
| Sidebar (trainer, admin) | `Sheet` lateral 312 px, overlay `#000103` a 64 %, slide 180 ms, botão fechar |
| Portal do cliente | Barra inferior de 4 itens, altura 64 px + safe-area 20 px; activo com ícone preenchido, texto e barra superior 2 px — nunca só cor |

## 3. Navegação por role (ajustada às rotas de `00_ARQUITETURA_FRONTEND.md` §6)

O mockup do trainer não tinha Check-ins nem Biblioteca e punha Marca própria e Subscrição
ao nível principal. Navegação aprovada para documentação:

### Trainer

| Grupo | Item | Rota |
|---|---|---|
| Principal | Painel | `/trainer` |
| | Clientes | `/trainer/clients` |
| | Sessões e packs | `/trainer/sessions` |
| | Check-ins | `/trainer/check-ins` |
| Prescrição | Planos de treino | `/trainer/training-plans` |
| | Planos alimentares | `/trainer/meal-plans` |
| | Biblioteca (exercícios · alimentos · suplementos) | `/trainer/library` |
| Definições | Marca própria | `/trainer/settings` |
| | Subscrição | `/trainer/billing` |

Removido: "Mensagens · Brevemente" (chat é futuro). Rotas finais fixadas no blueprint 6C/6E.

### Superuser

| Grupo | Item | Rota |
|---|---|---|
| Plataforma | Visão geral | `/admin` |
| | Moderação (fila, 6B) | `/admin/moderation` |
| Catálogos | Alimentos · Exercícios · Suplementos (com `total_count`) | `/admin/catalog/foods` · `/exercises` · `/supplements` |

### Cliente (barra inferior)

| Item | Rota | Conteúdo |
|---|---|---|
| Treino | `/portal/today` | Treino de hoje (6B) e registo de séries (6A) |
| Nutrição | `/portal/nutrition` | `GET /portal/my-nutrition` |
| Suplementos | `/portal/supplements` | `GET /portal/my-supplements` + tomas (6A) |
| Perfil | `/portal/profile` | `GET/PATCH /portal/my-profile`, avatar |

Check-ins pendentes aparecem como cartão na home do portal (`GET /portal/my-check-ins/due`)
e têm rota própria `/portal/check-ins`; não ocupam item da barra inferior.

## 4. Rodapé da sidebar — cartão de subscrição (trainer)

Mockup: "PLANO PRO · 18 CLIENTES · 18 de 25 clientes ativos". **Corrigido:**

- Fonte: `GET /billing/subscription` → `tier`, `client_limit`, `current_client_count`, `status`.
- `client_limit = null` (PRO) → "Plano PRO · 18 clientes · ilimitado", sem barra.
- Com limite (FREE 5, STARTER 25) → barra de progresso e "18 de 25 clientes".
- Estado de pagamento com problema → badge de aviso com ligação a Subscrição.

## 5. Topbar e dropdown de perfil (adoptado do v0)

| Elemento | Comportamento |
|---|---|
| Breadcrumbs | Gerados das rotas; último nível não clicável |
| Pesquisa | Botão "Pesquisar… ⌘K" (mostra `Ctrl K` em Windows/Linux) |
| Tema | Claro · escuro · sistema |
| Perfil | Avatar + nome (≥ 1024 px) + chevron → menu: nome e role; "O meu perfil" / "Definições"; tema; separador; **Terminar sessão** (`POST /auth/logout`, limpa sessão em memória e vai para `/auth/login`) |
| Notificações | Sem sino (DEF-PROD-005) |

## 6. Command palette ⌘K (artboard 08)

Mantém: overlay `#000103` a 55 % + blur 6 px; painel 640 px a 96 px do topo; glass
`#0A0F16` a 86 %; borda `rgba(0,163,233,.22)`; item activo com fundo `rgba(0,163,233,.14)`.

**Ajustes obrigatórios:**

1. Sem atalhos globais `⌘⇧S`, `⌘⇧T`, `⌘⇧A` (colidem com o browser). Ações executam-se
   só dentro da palette com Enter.
2. Grupos: **Ações** (por role), **Navegação**, **Clientes** (pesquisa `GET /clients?search=`
   com debounce 300 ms, a partir de 2 caracteres).
3. O contexto por cliente ("check-in de 11/09 por rever", "pack a terminar") vem do
   endpoint 6B; até lá mostra só nome e objetivo.
4. Rodapé: `↑↓ navegar · ↵ abrir · esc fechar`.
