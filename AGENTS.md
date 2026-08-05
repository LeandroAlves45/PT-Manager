# PT Manager

SaaS multi-tenant para personal trainers, com gestão de clientes, sessões,
packs, avaliações, planos de treino, nutrição, suplementos e billing.

Estado atual:

- O novo backend está em implementação em .NET 10 e C# 14 dentro de `backend/`
  (`backend/PTManager.sln`, projetos `Domain`, `Application`, `Infrastructure`,
  `Api` e respetivos projetos de teste já criados; Sprint 2 Infrastructure e
  EF Core concluído). Confirmar sempre o estado atual em
  `.codex/memory/MEMORY.md` antes de assumir o que já existe.
- O frontend existente usa React 19, Vite 7 e Tailwind CSS 4.
- `backend-python/` é apenas uma referência funcional local e está excluído do Git.

## Fontes de verdade

Consultar apenas os documentos relevantes para a tarefa:

- Arquitetura: `.claude/project/00_ARCHITECTURE.md`
- Base de dados: `.claude/project/01_DATABASE_SCHEMA.md`
- Roadmap: `.claude/project/02_SPRINTS_ROADMAP.md`
- Desenvolvimento local: `.claude/project/03_DEVELOPER_GUIDE.md`

O código Python e as respetivas migrations não definem a arquitetura de destino.

## Contexto no início de um chat

1. Ler este `AGENTS.md`.
2. Ler `.codex/memory/MEMORY.md` se o ficheiro existir.
3. Verificar `git status --short` antes de alterar ficheiros.
4. Ler apenas a documentação e o código necessários para a tarefa.
5. Se a memória contradizer os documentos canónicos ou o código atual, prevalecem
   os documentos canónicos e o código.

A memória é local, curta e auxiliar. Não criar um ficheiro de sessão em todas as
conversas. Atualizá-la apenas quando uma decisão ou limitação material mudar.

## Arquitetura do backend

- Modular monolith com projetos `Domain`, `Application`, `Infrastructure` e `Api`.
- `Domain` não depende de frameworks ou de outros projetos da solução.
- `Application` depende apenas de `Domain`.
- `Infrastructure` implementa as portas definidas pela Application.
- `Api` é o composition root.
- Organizar Application e Api por feature e caso de uso.
- Usar Controllers finos e handlers explícitos.
- Não introduzir repository genérico, Unit of Work genérico, MediatR ou AutoMapper.
- Usar `Result` e `Result<T>` para falhas esperadas.
- Converter erros para Problem Details na fronteira HTTP.
- Propagar `CancellationToken` em operações assíncronas de I/O.

## Contrato HTTP

- Preservar o prefixo `/api/v1`.
- Preservar JSON em `snake_case`.
- Classificar alterações de contrato como Preserve, Alias ou Remove.
- Não alterar payloads consumidos pelo frontend sem atualizar contrato, frontend e testes.
- Manter compatibilidade do campo `detail` nas respostas de erro.

## Autenticação e multi-tenancy

- Usar ASP.NET Core Identity.
- Access tokens JWT são curtos.
- Refresh tokens são opacos, rotativos e guardados apenas como hash.
- O access token fica em memória no frontend.
- O refresh token usa cookie HttpOnly, Secure e política SameSite adequada.
- Roles: `superuser`, `trainer` e `client`.
- O tenant efetivo vem do utilizador autenticado ou de contexto interno validado.
- Nunca confiar em `trainer_id` recebido no body, query string ou route.
- `ITenantContext` deve falhar de forma fechada quando o tenant é obrigatório.
- Aplicar Global Query Filters, validação de escritas, constraints e testes cross-tenant.
- Operações administrativas com bypass são explícitas, restritas e auditadas.
- Jobs, webhooks e cache transportam contexto de trainer explícito.

## Persistência e integrações

- PostgreSQL Neon é a fonte de verdade.
- EF Core gere o schema através de migrations novas.
- Nunca converter migrations Python.
- Nunca executar migrations automaticamente no arranque da API.
- Redis é apenas cache reconstruível e rate limiting. Nunca é fonte de autorização,
  sessão, billing ou jobs.
- QStash apenas ativa o dispatcher. Os jobs e a outbox persistem em PostgreSQL.
- RabbitMQ e MassTransit estão fora do MVP.
- Webhooks Stripe exigem raw body, assinatura, deduplicação por `event.id`,
  idempotência, reconciliação e outbox transacional.

## Ficheiros protegidos

Nunca ler, escrever ou incluir em output:

- `.env`, `.env.*`, `.env.local`, `.env.production`
- `**/*.pem`, `**/*.key`, `**/*.p12`, `**/*.pfx`
- `**/secrets/**`, `**/credentials/**`, `**/keys/**`

Não editar migrations existentes:

- `backend-python/app/db/migrations/**`
- `backend/src/Infrastructure/Data/Migrations/**`

Quando uma alteração de schema for autorizada, criar uma migration EF Core nova.
Não alterar uma migration já aplicada num ambiente partilhado.

Não modificar `backend-python/` salvo pedido explícito.

## Workflow

- Analisar e diagnosticar não autoriza implementar uma correção.
- Editar apenas quando o utilizador pedir uma alteração.
- Planear tarefas não triviais antes de implementar.
- Não criar ficheiros de gestão de tarefas sem pedido explícito.
- Preservar alterações existentes do utilizador e evitar trabalho fora do âmbito.
- Usar subagentes apenas para subtarefas independentes quando o runtime o permitir.
- Não alterar skills, hooks ou configurações pessoais sem pedido explícito.
- Usar branches de feature e conventional commits quando forem pedidos.

## Validação

Cada alteração deve ser validada proporcionalmente ao risco.

- Testar invariantes do Domain e handlers da Application.
- Usar PostgreSQL real ou Testcontainers em testes de persistência.
- Cobrir contratos HTTP, autenticação, multi-tenancy, Stripe e jobs.
- Incluir testes cross-tenant negativos desde o primeiro vertical slice.
- Correr architecture tests para as dependências entre projetos.
- Não usar uma percentagem global de cobertura como substituto dos cenários críticos.
- Não declarar uma tarefa concluída sem evidência verificável.

## Comandos

Frontend disponível atualmente, a partir de `frontend/`:

```bash
npm ci
npm run dev
npm run lint
npm run test -- --run
npm run build
```

Backend depois do scaffold, a partir de `backend/`:

```bash
dotnet restore PTManager.sln
dotnet build PTManager.sln --configuration Release --no-restore
dotnet test PTManager.sln --configuration Release --no-build
dotnet format PTManager.sln --verify-no-changes --no-restore
```

As instruções para criar migrations devem usar os caminhos efetivamente criados no
Sprint 0 e incluir sempre `--project` e `--startup-project`.

## Convenções

- Falar e documentar em Português de Portugal.
- Usar nomes de classes, métodos e propriedades em inglês.
- Sempre que eu pedir para criar files.md na pasta `docs` e que envolva código que seja criado em
 forma de pseudocódigo. Pseudocódigo alargado com XML Docs e comentários explicativos e no frontend com
 JSDOC e comentários explicativos. Código só fazes quando eu pedir especificamente para criar código real.
- Após algum pedido sempre auto-avaliar para analisar se está tudo correto e 100% certo e de acordo.
- Adicionar comentários sempre que sejam relevantes para a compreensão do código.
- Preferir soluções simples, legíveis e testáveis.
- Explicar decisões técnicas e respetivos trade-offs quando forem relevantes.
