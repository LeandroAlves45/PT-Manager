# PT Manager

SaaS multi-tenant para personal trainers — gestão de clientes, sessões, planos de treino, nutrição, suplementos e billing.

Backend em reescrita completa: Python 3.12/FastAPI/SQLModel → **C# 14 / .NET 10 / ASP.NET Core / EF Core 10**, modular monolith com Clean Architecture organizada por feature. Ver `.claude/project/00_ARCHITECTURE.md` (fonte de verdade da arquitetura), `01_DATABASE_SCHEMA.md` (schema alvo), `02_SPRINTS_ROADMAP.md` (plano de 12 semanas) e `03_DEVELOPER_GUIDE.md`.

`backend-python/` fica local, fora do Git, apenas como referência funcional durante a transição — **nunca** é o alvo da arquitetura nem recebe novas features.

Frontend: React 19, Vite 7, Tailwind CSS 4, Chakra UI + shadcn/ui (páginas `.jsx`, UI em `.tsx`) — inalterado pela migração do backend.

Deploy (MVP, todos free tier): Render (backend) + Vercel (frontend) + Neon (PostgreSQL 16) + Upstash (Redis + QStash).

Graph location: C:\Users\Leandro Alves\Desktop\Projeto pt_manager\Projeto_pt_manager\graphify-out

Quando analisares o projeto, consulta o graph para:

- Overview da Arquitetura
- Dependências de modulos
- Pontos-chave de conexões

## GOLDEN RULES

- Architecture: ver `.claude/project/00_ARCHITECTURE.md` (fonte de verdade). Camadas: `Api → Application → Domain ← Infrastructure`, organizadas por feature. Sem `IRepository<T>` genérico, sem MediatR/AutoMapper no MVP
- Multi-tenant: **todas** as queries filtram por `owner_trainer_id`, via EF Core Global Query Filters ligados a `ITenantContext` (nunca a `HttpContext` direto). Roles: `superuser`, `trainer`, `client`
- Memória: sistema persistente em `.claude/memory/` (índice `MEMORY.md` + notas `gotcha_*.md`). Cursor usa também claude-mem (ver `.cursor/hooks/README.md`)
- Base de dados: ver `.claude/project/01_DATABASE_SCHEMA.md`. Migrations **sempre geradas** via `dotnet ef migrations add` — nunca escritas à mão, nunca editar migration já aplicada num ambiente partilhado
- Segurança: ver `.claude/project/00_ARCHITECTURE.md §5-§7` (auth, tokens, multi-tenancy) — `security-conventions.md` foi removido, era específico do backend Python. Secrets em environment variables. Queries via EF Core (parametrizadas)
- Async/jobs: sem RabbitMQ/MassTransit no MVP — Upstash QStash ativa um dispatcher de `durable_jobs`/`outbox_messages` em Postgres (`00_ARCHITECTURE.md §9`)
- Testing: `dotnet test` (xUnit) no backend — unit, integration (Testcontainers) e functional (WebApplicationFactory). Vitest no frontend
- Git: feature branches, conventional commits, testes a passar antes de commit
- NO edits: migrations EF Core já aplicadas, ficheiros `.env`, valores hardcoded de secrets, código dentro de `backend-python/`
- Comentários e documentação em Português de Portugal
- Falar sempre em Português
- Avaliar sempre as respostas antes de as apresentar
- Ao documentar código: XML doc comments no backend C#, JSDoc no frontend. Métodos e classes em inglês
- Só editas código ou files se eu pedir especificamente.
- No final de cada sessão cria um file md em '.claude/memory/Sessions/' e coloca os pontos fundamentais da sessão
- Compacta sempre que atingir 50% da sessão
- Sugere mudar para os diferentes planos dependendo da task pedida.
- Sempre que eu precisar de tomar uma decisão técnica, oferece-me a melhor sugestão tendo em conta
  os trade-offs baseado em critérios técnicos, de DRY e Clean Code
- Sempre que eu pedir para criar files.md na pasta `docs` e que envolva código que seja criado em
 forma de pseudocódigo. Pseudocódigo alargado com XML Docs e comentários explicativos e no frontend com
 JSDOC e comentários explicativos. Código só fazes quando eu pedir especificamente para criar código real.
- Podes rodar comandos no terminal, exceto comandos destrutivos mencionados em hooks ou outro file relevante.
- Sempre que um finalizamos um sprint ou te pedir para pedir files md coloca uma checklist e depois
  de finaliza coloca "Finalizado"

## PROTECTED FILES — NUNCA LER OU ESCREVER

- `.env`, `.env.*`, `.env.local`, `.env.production`
- `**/*.pem`, `**/*.key`, `**/secrets/**`
- `**/src/Infrastructure/Migrations/**` — nunca editar migration EF Core já aplicada; criar sempre uma nova via `dotnet ef migrations add`
- `backend-python/**` — referência local, fora do Git; não é alvo de edição nem de novas features

Se uma tarefa exigir tocar nestes ficheiros, parar e pedir ao utilizador para o fazer manualmente.

## COMMANDS

```bash
# Backend C# (a partir de backend/)
dotnet run --project src/Api
dotnet ef database update --project src/Infrastructure
dotnet test
dotnet format

# Frontend (a partir de frontend/)
npm run dev
npm run test
npm run lint
npm run build
```

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

- Ao final e cada sessão, captura todos os erros, desafios e pontos de fricção encontrados que encontraste
  e coloca dentro de '.claude/memory/Memory.md'
- Depois de QUALQUER correção do utilizador: atualizar '.claude/tasks/correction.md'
  com o padrão do erro
- Se o problema da SKILL, ajusta a skill para o projeto
- Sempre usa o folder '.claude' no começo da sessão
- Escrever regras próprias que previnam o mesmo erro
- Iterar sem piedade nas lições até a taxa de erro baixar
- Rever lessons no início da sessão para o projeto relevante

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

1. **Plan First**: Escrever o plano em `.claude/tasks/todo.md` com items marcáveis
2. **Verify Plan**: Confirmar com o utilizador antes de implementar
3. **Track Progress**: Marcar items como concluídos à medida que avança
4. **Explain Changes**: Resumo de alto nível a cada passo
5. **Document Results**: Adicionar secção de review a `tasks/todo.md`
6. **Capture Lessons**: Atualizar `tasks/lessons.md` depois de correções

## Core Principles

- **Simplicity First**: Cada alteração o mais simples possível. Impacto mínimo no código
- **No Laziness**: Encontrar causas raiz. Sem soluções temporárias. Padrão de senior developer
- **Minimal Impact**: Alterações tocam apenas no necessário. Evitar introduzir bugs
