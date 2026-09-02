# PT Manager — Claude Code

Regras técnicas transversais (arquitetura, contrato HTTP, segurança, multi-tenancy, base de dados, testing, git, ficheiros protegidos, comandos) estão em `AGENTS.md`, sempre válidas para qualquer agente. Este ficheiro cobre apenas comportamento específico do Claude Code.

Deploy (MVP, todos free tier): Render (backend) + Vercel (frontend) + Neon (PostgreSQL 17) + Upstash (Redis + QStash).

Graph location: C:\Users\Leandro Alves\Desktop\Projeto pt_manager\Projeto_pt_manager\graphify-out

Regenerar no fecho de cada Sprint — ver `.claude/project/sprints/GRAPHIFY.md`.

## Contexto de sprint (ler primeiro)

1. `.claude/memory/ACTIVE.md` — fase activa e sub-lote
2. Sprint Pack em `.claude/project/sprints/`
3. Skill `sprint-context` para plan, blueprint ou review

## Memória

Sistema persistente em `.claude/memory/` (índice `MEMORY.md` + notas de sessão em `Sessions/`), complementado pelo plugin claude-mem.
No final de cada sessão, criar um ficheiro md em `.claude/memory/Sessions/` com os pontos fundamentais.
Rever `.claude/tasks/lessons.md` e a memória relevante no início da sessão.

## Comportamento de sessão

- Falar sempre em Português. Avaliar sempre as respostas antes de as apresentar.
- Só editar código ou ficheiros se pedido especificamente.
- Correr comandos no terminal é permitido, exceto comandos destrutivos mencionados em hooks.
- Ao criar ficheiros md na pasta `docs` com código integral: incluir explicações detalhadas sobre o funcionamento do código e comentários relevantes. XML docs completos no backend, JSDoc no frontend.
- Sempre que finalizarmos um sprint, ou for pedido, apresentar checklist e marcar "Finalizado" ao concluir.
- Não agir por suposições; sempre verificar fatos e confirmar informações antes de tomar decisões.

## WORKFLOW ORCHESTRATION

### 1. Plan Mode Default

- Entrar em plan mode para qualquer tarefa não trivial (3+ passos ou decisões arquiteturais)
- Se algo correr mal, PARAR e replanear imediatamente — não insistir no caminho errado
- Usar plan mode também para passos de verificação, não só para construir
- Escrever specs detalhadas à partida para reduzir ambiguidade

### 2. Subagent Strategy

- Usar subagentes livremente para manter a context window principal limpa
- Delegar pesquisa, exploração e análise paralela a subagentes
- Para problemas complexos, atirar mais compute via subagentes
- Uma tarefa por subagente para execução focada

### 3. Self-improvement Loop

- Ao final de cada sessão, captura erros, desafios e pontos de fricção encontrados e coloca-os
  em `.claude/memory/Sessions/` (ver secção Memória acima)
- Depois de QUALQUER correção do utilizador: atualizar `.claude/tasks/correction.md`
  com o padrão do erro
- Se o problema for da SKILL, ajusta a skill para o projeto
- Escrever regras próprias que previnam o mesmo erro
- Iterar sem piedade nas lições até a taxa de erro baixar

### 4. Verification Before Done

- Nunca marcar uma tarefa como concluída sem provar que funciona
- Comparar comportamento entre main e as alterações quando relevante
- Perguntar: "um staff engineer aprovaria isto?"
- Correr testes, verificar logs, demonstrar correção

### 5. Demand Elegance (Balanced)

- Para alterações não triviais: parar e perguntar "há uma forma mais elegante?"
- Se uma correção parecer um hack: "Sabendo tudo o que sei agora, implementa a solução elegante"
- Saltar isto para correções simples e óbvias, não sobre-engenheirar
- Desafiar o próprio trabalho antes de o apresentar

### 6. Autonomous Bug Fixing

- Perante um bug report: corrigir diretamente, sem pedir ajuda passo a passo
- Apontar para logs, erros, testes a falhar, depois resolver
- Zero context switching exigido ao utilizador
- Corrigir testes de CI a falhar sem instruções detalhadas

## Task Management

1. **Plan**: Escrever o plano em `.claude/tasks/todo.md` com items marcáveis (ver Plan Mode Default acima)
2. **Track Progress**: Marcar items como concluídos à medida que avança
3. **Explain Changes**: Resumo de alto nível a cada passo
4. **Document Results**: Adicionar secção de review a `tasks/todo.md`

## Core Principles

- **Simplicity First**: Cada alteração o mais simples possível. Impacto mínimo no código
- **No Laziness**: Encontrar causas raiz. Sem soluções temporárias. Padrão de senior developer
- **Minimal Impact**: Alterações tocam apenas no necessário. Evitar introduzir bugs
