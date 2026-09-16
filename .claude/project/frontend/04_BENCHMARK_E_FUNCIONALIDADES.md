# Benchmark e Mapa de Funcionalidades

*Pesquisa de 2026-09-15. Serve para orientar o design, não para alargar o âmbito do MVP.*

## 1. Fontes

- UpCoach (Portugal, Lisboa): [upcoach.pt](https://upcoach.pt/),
  [App Store](https://apps.apple.com/pt/app/upcoach/id6505041606).
- Comparação Everfit/Trainerize/TrueCoach:
  [blog.everfit.io](https://blog.everfit.io/everfit-vs-trainerize-vs-truecoach).

Limitação: não foi possível capturar ecrãs da app da UpCoach (login privado e captura
bloqueada). A análise visual fica pendente de screenshots fornecidos pelo utilizador.

## 2. O que a UpCoach oferece

Planos de 7 €/mês (5 clientes) a 99 €/mês (ilimitados), 7 dias grátis. Funcionalidades
anunciadas: planos de treino, nutrição e suplementação; gestão de clientes; avaliação de
progresso e métricas corporais; hábitos diários; preferências alimentares; calculadora
TDEE; chat 1-1, de grupo e de comunidade; updates personalizados e automações; pagamentos
automáticos; notificações push; marca própria (nome, logo, cores) a partir do plano
Profissional; staff com níveis de acesso; lista de tarefas; calendário e marcações; cofre
de ficheiros; cursos; app do cliente.

## 3. O que os treinadores mais valorizam (Everfit/Trainerize/TrueCoach)

- Construtor de treinos rápido (drag & drop, atalhos, pesquisa na biblioteca).
- Visão semanal/mensal do programa.
- Check-ins automáticos e gráficos de progresso.
- App do cliente unificada (treino, nutrição, progresso) com marca do treinador.
- Pagamentos integrados e análise de receita/churn.

## 4. Matriz: funcionalidade × backend PT Manager

| Funcionalidade | UpCoach | Backend PT Manager | Frontend |
|---|---|---|---|
| Gestão de clientes (criar, editar, arquivar, reativar) | ✅ | ✅ `/clients` | 6C |
| Convite do cliente e primeiro acesso | ✅ | ✅ `/auth/invite-client`, `/auth/accept-invite` | 6C · 6E |
| Avaliação inicial | ✅ | ✅ `/initial-assessments` | 6C |
| Planos de treino e estrutura | ✅ | ✅ `/training-plans`, `/structure` | 6C |
| Biblioteca de exercícios (global + privada) | ✅ | ✅ `/global-exercises`, `/exercises` | 6B · 6C |
| Registo de séries | ✅ | ✅ trainer `/exercise-set-logs` (cliente: DEF-PORTAL-001) | 6C |
| Planos alimentares e cálculo nutricional | ✅ | ✅ `/meal-plans`, `/nutrition/preview` | 6C |
| Base de alimentos (global + privada) | ✅ | ✅ `/global-foods`, `/foods` | 6B · 6C |
| Planos de suplementação | ✅ | ✅ `/supplements`, `/supplement-assignments` | 6C |
| Check-ins | ✅ | ✅ `/check-ins`, `/portal/check-ins/*/respond` | 6C · 6D |
| Sessões presenciais e packs | parcial (calendário) | ✅ `/sessions`, `/pack-types`, `/client-session-packs` | 6C |
| Billing SaaS do trainer | ✅ | ✅ Stripe `/billing/*` | 6C |
| Marca própria do trainer | ✅ (plano 69 €) | ✅ `/trainer-settings/branding`, `/logo`, `/portal/branding` | 6C · 6D |
| Avatar do cliente moderado | — | ✅ `/portal/my-profile/avatar` | 6D |
| Moderação de conteúdo | — | ✅ `/admin/content-moderation/*` | 6B |
| Vídeo de exercício privado | ✅ (vídeos) | 🔄 Fase 5D (R2) | 6C após 5D |
| Chat 1-1 / grupo / comunidade | ✅ | ❌ | **Futuro** |
| Calendário e marcações pelo cliente | ✅ | ❌ (cancelamento pelo cliente: DEF-PORTAL-002) | **Futuro** |
| Hábitos diários | ✅ | ❌ | **Futuro** |
| Métricas corporais personalizáveis | ✅ | ❌ (DEF-PROD-001) | **Futuro** |
| Fotos de progresso | ✅ | ❌ | **Futuro** |
| Automações de acompanhamento | ✅ | ❌ (só jobs internos) | **Futuro** |
| Staff e níveis de acesso | ✅ | ❌ | **Futuro** |
| Lista de tarefas | ✅ | ❌ | **Futuro** |
| Cofre de ficheiros / conteúdos | ✅ | ❌ | **Futuro** |
| Cursos | ✅ | ❌ | **Futuro** |
| Notificações push | ✅ | ❌ (emails transacionais existem) | **Futuro** |
| AI meal planner / scanner | concorrentes | ❌ | **Futuro** |
| Relatórios persistidos | — | ❌ (DEF-PROD-003) | **Futuro** |

Regra: tudo o que está marcado **Futuro** não aparece como ecrã funcional. Pode existir,
no máximo, um item de navegação desativado com etiqueta "Brevemente", se o utilizador o
aprovar por ecrã.

## 5. Diferenciais do PT Manager (objetivo de design)

1. **Visual moderno e apelativo** onde a concorrência portuguesa é genérica: dark mode de
   performance, bento dashboards, micro-interações cuidadas.
2. **Marca do trainer desde o primeiro dia** no portal do cliente (logo e cores via
   endpoints existentes), não reservada a um plano caro.
3. **Dashboard do trainer orientado à ação:** em vez de números soltos, alertas com CTA —
   check-ins por rever, packs de sessões a terminar, planos a expirar, sessões de hoje,
   clientes sem plano ativo.
4. **Nutrição séria:** cálculo com preview (`/nutrition/preview`) e escolha de alimentos por
   pesquisa rápida, alinhado com o posicionamento de trainers que prescrevem nutrição.
5. **Confiança:** moderação de conteúdo, avatar moderado e isolamento multi-tenant visíveis
   como qualidade do produto, não como fricção.
