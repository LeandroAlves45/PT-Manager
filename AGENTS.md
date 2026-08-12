# PT Manager

SaaS multi-tenant para personal trainers com gestão de clientes, sessões, packs,
avaliações, planos de treino, nutrição, suplementos e billing.

## Estado atual

- O backend novo está em implementação em .NET 10 e C# 14 em `backend/`.
- A solução inclui `Domain`, `Application`, `Infrastructure`, `Api` e testes.
- O frontend usa React 19, Vite 7 e Tailwind CSS 4.
- `backend-python/` é apenas referência funcional local e não é alvo do projeto atual.

## Prioridade de decisão

Quando houver conflito de informação, aplicar esta ordem:

1. Pedido explícito do utilizador.
2. Este ficheiro `AGENTS.md`.
3. Documentos canónicos: `.claude/project/00_ARCHITECTURE.md`,
   `.claude/project/01_DATABASE_SCHEMA.md`, `.claude/project/02_SPRINTS_ROADMAP.md`,
   `.claude/project/03_DEVELOPER_GUIDE.md`.
4. Código atual do repositório.
5. `.codex/memory/MEMORY.md` e memória local.

A memória é auxiliar; não substitui arquitetura, contrato nem código verificado.

## Fontes de verdade

Usar sempre os documentos relevantes para a tarefa e evitar informação desatualizada.

- Arquitetura: `.claude/project/00_ARCHITECTURE.md`
- Base de dados: `.claude/project/01_DATABASE_SCHEMA.md`
- Roadmap: `.claude/project/02_SPRINTS_ROADMAP.md`
- Desenvolvimento local: `.claude/project/03_DEVELOPER_GUIDE.md`

O código Python e as migrations antigas não definem a arquitetura de destino.

## Início de sessão e investigação

1. Ler este `AGENTS.md`.
2. Ler `.codex/memory/MEMORY.md` se existir.
3. Verificar `git status --short` antes de alterar ficheiros.
4. Ler apenas a documentação e o código estritamente necessários.
5. Se a memória contradisser o código atual ou os documentos canónicos, prevalecem
   os documentos e o código atual.

## Arquitetura obrigatória

- Modular monolith com projetos `Domain`, `Application`, `Infrastructure` e `Api`.
- `Domain` não depende de frameworks nem de outros projetos da solução.
- `Application` depende apenas de `Domain`.
- `Infrastructure` implementa as portas definidas pela `Application`.
- `Api` é o composition root e mantém controladores finos.
- Organizar `Application` e `Api` por feature e por caso de uso.
- Não introduzir `IRepository<T>` genérico, `UnitOfWork` genérico, MediatR ou AutoMapper.
- Usar `Result` e `Result<T>` para falhas esperadas.
- Converter erros para `Problem Details` na fronteira HTTP.
- Propagar `CancellationToken` em operações assíncronas de I/O.
- Preferir a solução mais simples e testável; evitar abstrações sem necessidade.

## Contrato HTTP

- Preservar o prefixo `/api/v1`.
- Preservar JSON em `snake_case`.
- Classificar alterações de contrato como `Preserve`, `Alias` ou `Remove`.
- Não alterar payloads consumidos pelo frontend sem atualizar contrato, frontend e testes.
- Manter compatibilidade do campo `detail` nas respostas de erro.
- Evitar mudanças de contrato sem necessidade ou sem documentação explícita.

## Segurança e multi-tenancy

- Usar ASP.NET Core Identity.
- Access tokens JWT curtos.
- Refresh tokens opacos, rotativos e guardados apenas como hash.
- O access token fica em memória no frontend.
- O refresh token usa cookie `HttpOnly`, `Secure` e política `SameSite` adequada.
- Roles: `superuser`, `trainer` e `client`.
- O tenant efetivo vem do utilizador autenticado ou de contexto interno validado.
- Nunca confiar em `trainer_id` recebido no body, query string ou route.
- `ITenantContext` deve falhar de forma fechada quando o tenant for obrigatório.
- Aplicar Global Query Filters, validação de escritas, constraints e testes cross-tenant.
- Operações administrativas com bypass são explícitas, restritas e auditadas.
- Jobs, webhooks e cache transportam contexto de trainer explícito.

## Persistência e integrações

- PostgreSQL Neon é a fonte de verdade.
- EF Core gere o schema por migrações novas.
- Nunca converter migrations Python.
- Nunca executar migrations automaticamente no arranque da API.
- Redis é apenas cache reconstruível e rate limiting; nunca é fonte de autorização,
  sessão, billing ou jobs.
- QStash apenas ativa o dispatcher; jobs e outbox persistem em PostgreSQL.
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

Quando uma alteração de schema for autorizada, criar uma migration EF Core nova e
não alterar uma migration já aplicada num ambiente partilhado.

Não modificar `backend-python/` salvo pedido explícito.

## Workflow de trabalho

- Diagnosticar antes de corrigir.
- Editar apenas quando o utilizador pedir uma alteração.
- Planear tarefas não triviais antes de implementar.
- Preservar alterações existentes do utilizador e evitar trabalho fora do âmbito.
- Não criar ficheiros de gestão de tarefas sem pedido explícito.
- `docs/` contém documentação local deliberadamente ignorada pelo Git. Nunca usar
  `git add -f` para versionar ficheiros deste diretório.
- Usar subagentes apenas para subtarefas independentes quando o runtime o permitir.
- Não alterar skills, hooks ou configurações pessoais sem pedido explícito.
- Usar branches de feature e conventional commits quando forem pedidos.
- Não fazer refactors ou abstrações que não sejam necessárias para a tarefa atual.

## Regras de implementação

- Só mexer no que é necessário para resolver o pedido.
- Não assumir requisitos que não foram explicitados.
- Não “melhorar” áreas fora do escopo.
- Não criar utilitários genéricos antes de existir um caso real de uso.
- Quando existir incerteza, buscar a fonte de verdade antes de implementar.
- Se a solução parecer um hack, reavaliar a arquitetura e simplificar.

## Validação mínima exigida

Cada alteração deve ser validada proporcionalmente ao risco.

- Testar invariantes do `Domain` e handlers da `Application`.
- Usar PostgreSQL real ou Testcontainers em testes de persistência.
- Cobrir contratos HTTP, autenticação, multi-tenancy, Stripe e jobs.
- Incluir testes cross-tenant negativos desde o primeiro vertical slice.
- Correr architecture tests para as dependências entre projetos.
- Não usar uma percentagem global de cobertura como substituto dos cenários críticos.
- Não declarar uma tarefa concluída sem evidência verificável.
- Na conclusão, confirmar explicitamente o que foi validado e com que comando/resultado.

## Comandos

Frontend, a partir de `frontend/`:

```bash
npm ci
npm run dev
npm run lint
npm run test -- --run
npm run build
```

Backend, a partir de `backend/`:

```bash
dotnet restore PTManager.sln
dotnet build PTManager.sln --configuration Release --no-restore
dotnet test PTManager.sln --configuration Release --no-build
dotnet format PTManager.sln --verify-no-changes --no-restore
```

As instruções para criar migrations devem usar os caminhos efetivamente criados no
Sprint 0 e incluir sempre `--project` e `--startup-project`.

## Regras práticas para agentes

- Falar e documentar em Português de Portugal.
- Usar nomes de classes, métodos e propriedades em inglês.
- Explicar decisões técnicas e trade-offs quando relevantes.
- Adicionar comentários apenas quando ajudam à compreensão.
- Preferir soluções simples, legíveis e testáveis.
- Ao criar ficheiros `docs` com pseudocódigo, manter comentários explicativos em XML Docs
  (backend) ou JSDoc (frontend), sem implementar código real sem pedido explícito.
- Após qualquer tarefa, auto-avaliar se a solução está correta, coerente e dentro do escopo.

## Critério de “done”

Uma tarefa só está concluída quando:

- resolve o problema pedido;
- respeita arquitetura, contrato e regras de segurança;
- não introduz regressões fora do escopo;
- foi validada com teste, build ou verificação relevante;
- o resultado foi explicado com impacto, trade-offs e decisões tomadas.

## Não fazer

- Não mexer em ficheiros protegidos.
- Não converter ou reescrever código Python para alvo da arquitetura atual.
- Não criar abstrações, frameworks ou utilitários genéricos sem necessidade.
- Não fazer refactors grandes fora do âmbito solicitado.
- Não assumir que um bug é pequeno quando envolve autenticação, tenant, billing ou schema.
- Não alterar a funcionalidade de um endpoint ou payload sem atualizar frontend, testes e documentação relevantes.
