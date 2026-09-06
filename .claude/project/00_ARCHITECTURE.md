# PT Manager: Arquitectura do Backend C# v3.0

Documento de arquitectura para a reescrita do backend em C# e .NET 10.

Estado: arquitetura aprovada. Domain e Infrastructure implementados até ao
fecho do Sprint 2 em 5 de agosto de 2026. Application e Api continuam a ser
desenvolvidos nos sprints seguintes.

Data de revisão: 5 de agosto de 2026.

## 1. Objectivo e contexto

O PT Manager é uma aplicação SaaS multi-tenant para personal trainers gerirem clientes, sessões, planos de treino, nutrição, suplementos e billing.

O backend Python será substituído por um backend C# novo. O directório local `backend-python/` permanece temporariamente como referência funcional e está excluído do Git. O código Python, as suas abstracções e o histórico das suas migrations não constituem a arquitectura de destino.

A primeira versão do backend C# assume:

1. Um MVP com até 100 trainers.
2. Nenhum dado de produção a preservar.
3. Compatibilidade funcional com o frontend existente.
4. Backend alojado no plano gratuito do Render.
5. Frontend alojado na Vercel.
6. PostgreSQL alojado no Neon.
7. Cache local e rate limiting em processo no MVP; HybridCache com Upstash Redis
   fica sujeito ao Gate 6B e só é implementado com necessidade medida.
8. Upstash QStash para activar processamento assíncrono.
9. Atrasos até cerca de vinte minutos, acrescidos de cold start, são aceitáveis nos lembretes do MVP gratuito.

## 2. Decisões arquitecturais

### 2.1 Modular monolith

O backend será um modular monolith. Os módulos funcionais partilham o mesmo processo e a mesma base de dados, mas mantêm limites explícitos no código.

Esta decisão oferece:

1. Transacções locais simples.
2. Deploy e diagnóstico adequados a uma equipa pequena.
3. Menor custo operacional do que microserviços.
4. Separação suficiente para extrair um módulo no futuro, caso exista uma necessidade comprovada.

Microserviços não fazem parte do MVP. Um módulo só deve ser extraído quando existirem requisitos mensuráveis de escalabilidade, disponibilidade, ownership ou deploy independente. Esta reavaliação está registada no Sprint 9B, sem compromisso de implementação.

### 2.2 Clean Architecture

A solução terá quatro projectos de produção:

1. `Domain`
2. `Application`
3. `Infrastructure`
4. `Api`

As dependências de compilação são:

```text
Domain
    Não depende de outros projectos da solução

Application
    Depende de Domain

Infrastructure
    Depende de Application e Domain
    Implementa as portas declaradas pela Application

Api
    Depende de Application
    Referencia Infrastructure apenas no composition root
```

O fluxo de execução de um pedido não altera estas dependências:

```text
HTTP Request
    -> Controller
    -> Application Handler
    -> Porta da Application
    -> Implementação da Infrastructure
    -> PostgreSQL ou serviço externo
```

O `Program.cs` é o composition root. Deve chamar métodos de registo como `AddApplication` e `AddInfrastructure`, sem espalhar tipos concretos da Infrastructure pela API.

### 2.3 Organização por feature

A API e a Application são organizadas por domínio funcional e caso de uso, não por pastas horizontais globais com dezenas de classes sem relação imediata.

Exemplos de features:

1. Authentication
2. Trainers
3. Clients
4. Sessions
5. TrainingPlans
6. Nutrition
7. Supplements
8. Assessments
9. Billing
10. Notifications
11. Administration

Cada operação da Application tem um handler explícito. Por exemplo, criar, actualizar e arquivar um cliente são casos de uso distintos. Não será introduzido MediatR no MVP.

Os Controllers:

1. Recebem e validam o contrato HTTP.
2. Obtêm o utilizador autenticado através do contexto aprovado.
3. Chamam um único handler por operação.
4. Convertem o resultado da Application numa resposta HTTP.
5. Não executam queries EF Core nem regras de negócio.

A API valida a estrutura do contrato HTTP, incluindo tipos, campos obrigatórios e limites de tamanho. A Application valida o caso de uso e as regras que devem ser aplicadas independentemente do entry point. A validação da Application é explícita e assíncrona, pelo que também se aplica a jobs, webhooks e futuros adapters.

### 2.4 Abstracções explícitas

Não será criado um `IRepository<T>` genérico que replique `DbSet<T>`. As portas de persistência devem representar necessidades dos casos de uso.

Exemplos de portas da Application:

1. `ITenantContext`
2. `IAuthenticationEmailSender`
3. `ICheckoutGateway`
4. `ICustomerPortalGateway`
5. `ISubscriptionReconciliationGateway`
6. `IMediaStorage`
7. `IClock`
8. Repositórios ou query services específicos por agregado ou feature

Uma porta de cache ou de outra integração só é criada quando existir um consumidor
real. Os nomes acima documentam contratos já materializados e não antecipam serviços
genéricos sem caso de uso.

O `DbContext` representa a unidade transaccional na Infrastructure. Uma abstracção adicional de Unit of Work só deve existir se resolver uma necessidade concreta que não seja já satisfeita pelo EF Core.

MediatR e AutoMapper não são adotados neste projeto. O dispatch dos handlers e o
mapping entre entidades, resultados e contratos são explícitos. Esta é uma decisão
arquitectural rejeitada, não uma implementação adiada.

## 3. Estrutura do monorepo

A estrutura de destino é:

```text
/
|-- .github/
|   `-- workflows/
|       |-- ci.yml
|       `-- deploy.yml
|
|-- backend/
|   |-- PTManager.sln
|   |-- Directory.Build.props
|   |-- Directory.Packages.props
|   |-- Dockerfile
|   |-- .dockerignore
|   |
|   |-- src/
|   |   |-- Domain/
|   |   |-- Application/
|   |   |-- Infrastructure/
|   |   `-- Api/
|   |
|   `-- tests/
|       |-- Unit/
|            |-- Domain.UnitTests/
|            |-- Application.UnitTests/
|       |-- Integration/
|            |-- Infrastructure.IntegrationTests/
|       |-- FunctionalTests/
|            |-- Api.FunctionalTests/
|       `-- ArchitectureTests/
|
|-- frontend/
|
`-- backend-python/
    Referência local excluída do Git
```

Os workflows pertencem à raiz do monorepo. O Dockerfile do backend pertence a `backend/`.

Antes de criar a solução, o Sprint 0 deve confirmar que o `.gitignore` não ignora ficheiros `*.sln`. A pasta local `docs/` permanece deliberadamente ignorada e essa regra não deve ser removida.

## 4. Contrato HTTP e compatibilidade

### 4.1 Contrato preservado

A primeira versão C# preserva:

1. Prefixo `/api/v1`.
2. JSON em `snake_case`.
3. Roles `superuser`, `trainer` e `client`.
4. Formatos de resposta consumidos pelo frontend.
5. Semântica actual de HTTP 402 para bloqueios de subscrição.
6. Endpoints de trainer, cliente, administração, billing e portal do cliente que estejam efectivamente em uso.

A compatibilidade é comportamental. Bugs de segurança, rotas quebradas e controlos que não oferecem segurança real não são contratos a preservar.

### 4.2 Matriz de migração

Antes de implementar cada feature, os endpoints correspondentes devem ser classificados:

| Classificação | Utilização |
|---|---|
| Preserve | Rota, método, payload e resposta continuam iguais |
| Alias | A API mantém temporariamente a forma antiga e define uma forma canónica |
| Remove | Comportamento inseguro, quebrado ou comprovadamente não utilizado |

Exemplos que exigem confirmação por contract tests:

1. Rotas de signup com prefixos divergentes.
2. Rotas de compras de packs com formatos divergentes.
3. Operações em que o frontend usa `PUT` e o backend Python expõe `PATCH`.
4. Variações de trailing slash.

O OpenAPI gerado pelo backend C# será a fonte de verdade do contrato novo. A matriz de migração e os contract tests impedem que inconsistências Python sejam copiadas sem análise.

### 4.3 Erros HTTP

A Application retorna `Result` ou `Result<T>` para falhas esperadas.

Um erro esperado contém, no mínimo:

1. Código estável.
2. Categoria.
3. Descrição segura para o cliente.
4. Metadados estritamente necessários.

Categorias previstas:

1. Validation
2. NotFound
3. Conflict
4. Unauthorized
5. Forbidden
6. PaymentRequired
7. ExternalDependency

A API converte estes erros para Problem Details. Durante a compatibilidade com o frontend existente:

1. A propriedade `detail` continua disponível.
2. Erros de validação 422 mantêm temporariamente a forma esperada pelo interceptor actual.
3. Nenhuma resposta expõe stack traces, secrets ou detalhes internos.

Excepções representam falhas inesperadas ou indisponibilidade técnica. Um middleware global converte excepções não tratadas para respostas seguras e envia o erro para observabilidade.

### 4.4 Operações assíncronas

Todas as operações de I/O devem:

1. Ser assíncronas.
2. Receber e propagar `CancellationToken`.
3. Evitar `.Result`, `.Wait()` e sync-over-async.
4. Definir timeouts para PostgreSQL, Redis e chamadas HTTP externas.

`async` não garante, por si só, que uma operação nunca bloqueia uma thread. Bibliotecas síncronas e trabalho CPU-bound devem ser tratados explicitamente.

## 5. Autenticação e autorização

### 5.1 ASP.NET Core Identity

ASP.NET Core Identity será responsável por:

1. Hash de passwords.
2. Políticas de password.
3. Lockout.
4. Verificação de email.
5. Estado activo ou suspenso do utilizador.
6. Roles.

Não será portada uma implementação própria de hashing do backend Python.

### 5.2 Access e refresh tokens

O fluxo aprovado é:

1. Access token JWT com duração curta, inicialmente 15 minutos e configurável.
2. Refresh token opaco com duração inicial de 30 dias e configurável.
3. Apenas o hash do refresh token é persistido no PostgreSQL.
4. Cada refresh roda o token.
5. Reutilização de um token rodado revoga a família correspondente.
6. Logout revoga a sessão no servidor.

O refresh token é enviado num cookie:

1. `HttpOnly`
2. `Secure`
3. `SameSite=None` enquanto frontend e API estiverem em sites diferentes
4. Path limitado aos endpoints de autenticação aplicáveis

O access token é mantido em memória no frontend. No arranque da aplicação, o frontend tenta renovar a sessão através do cookie. Pedidos de refresh usam credentials e CORS permite apenas origens aprovadas.

A lista de origens aprovadas é explícita em configuração (não wildcard): o domínio de produção da Vercel e os domínios de preview deployment gerados por PR, confirmados e mantidos no Sprint 0.

`SameSite=None` permite o envio cross-site, mas não contorna políticas do browser que bloqueiem cookies de terceiros. Antes de produção comercial, frontend e API devem usar subdomínios do mesmo site registável. Se isso não for possível, deve ser avaliado um Backend for Frontend que mantenha a sessão no mesmo site do frontend.

Endpoints que alterem ou renovem uma sessão através de cookie, incluindo refresh e logout, exigem ainda:

1. Validação estrita do header `Origin`.
2. Token anti-CSRF associado à sessão e enviado num header próprio.
3. Rejeição de pedidos sem origem aprovada ou sem token válido.
4. Testes funcionais de pedidos cross-site não autorizados.

CORS não substitui protecção CSRF.

### 5.3 Claims e políticas

O JWT pode transportar identificadores e role necessários à autorização. Pedidos normais validam assinatura, issuer, audience e expiração.

O refresh consulta sempre PostgreSQL e valida utilizador, sessão, email e estado de suspensão. Logout revoga o refresh token e o frontend elimina o access token em memória. Um access token já emitido pode permanecer tecnicamente válido até ao máximo de 15 minutos definido para a sua expiração.

Operações de risco elevado, como administração global e alterações de billing, voltam a validar o estado actual do utilizador no PostgreSQL. Se no futuro for necessária revogação global imediata de access tokens, deve ser introduzida uma versão de sessão ou security stamp validada no servidor. Esta decisão é reavaliada no Sprint 9B a partir de requisitos de risco e compliance.

As políticas distinguem:

1. Autenticação.
2. Role.
3. Tenant.
4. Ownership do recurso.
5. Estado da subscrição.

O `trainer_id` recebido num body, query string ou route parameter nunca define o tenant efectivo do pedido.

Uma API key incorporada no frontend não é um secret e não constitui controlo de segurança. Caso o header legado seja tolerado durante a migração, não pode ser tratado como autenticação.

## 6. Isolamento multi-tenant

### 6.1 TenantContext

`ITenantContext` é scoped e contém o contexto necessário para a operação actual:

1. Trainer efectivo.
2. Utilizador.
3. Role.
4. Origem da execução, como HTTP, QStash ou Stripe.
5. Indicação explícita de operação administrativa aprovada.

Um tenant em falta provoca falha fechada nas operações tenant-owned. `null` nunca significa acesso global.

### 6.2 Queries EF Core

As entidades tenant-owned usam Global Query Filters centralizados no `PtManagerDbContext`. O trainer efectivo deve ser uma propriedade da instância do `DbContext`, obtida a partir de `ITenantContext`. As `IEntityTypeConfiguration<T>` configuram mapping, constraints, índices e relações, mas não capturam o tenant.

Não se deve resolver `trainer_id` dentro de `OnModelCreating` através de `HttpContext`. O modelo EF Core é reutilizado e jobs ou webhooks podem não ter contexto HTTP.

Dados globais e privados, como catálogos, usam uma política explícita equivalente a:

```text
OwnerTrainerId é null
ou
OwnerTrainerId é igual ao TrainerId efectivo
```

O filtro de catálogo também exige que o trainer efectivo exista. Sem tenant,
nenhuma linha é devolvida, incluindo as globais.

As entidades filhas de agregado têm navegações POCO dependente-para-raiz e
filtros equivalentes através da raiz. Isto protege queries diretas às filhas sem
duplicar `owner_trainer_id`.

`IgnoreQueryFilters` é proibido no código funcional normal. Uma utilização administrativa exige:

1. Caso de uso dedicado.
2. Política de autorização própria.
3. Auditoria.
4. Teste cross-tenant.

### 6.3 Escritas e integridade

Os query filters não protegem escritas. A Infrastructure deve:

1. Atribuir o trainer efectivo na criação de entidades tenant-owned.
2. Rejeitar alterações que tentem trocar o tenant.
3. Validar entidades adicionadas ou modificadas no
   `TenantWriteValidationInterceptor`. Validações com I/O correm em
   `SavingChangesAsync`; a variante síncrona falha explicitamente.
4. Aplicar foreign keys e constraints que impeçam relações entre tenants quando o schema o permitir.

A forma concreta das constraints será definida em `01_DATABASE_SCHEMA.md`.

### 6.4 Jobs, webhooks e cache

Um job tenant-owned transporta o `TrainerId` persistido. O dispatcher cria um scope, valida o trainer e constrói `ITenantContext` antes de chamar o handler.

O owner de um lease é um token opaco novo por execução de claim. Renovação,
conclusão e falha exigem estado `Processing`, token correspondente e lease ainda
ativo. O claim usa uma transação curta com `SELECT ... FOR UPDATE SKIP LOCKED`,
transição no Domain, `SaveChangesAsync` e commit.

Um webhook Stripe resolve o trainer a partir de identificadores persistidos e validados. Metadata recebida da Stripe não concede autorização por si só.

As cache keys tenant-owned seguem o formato conceptual:

```text
{environment}:trainer:{trainer_id}:{feature}:{resource}
```

### 6.5 Superuser e RLS

O superuser não obtém acesso global através de um tenant vazio. Operações globais usam handlers, políticas e contexto administrativo explícitos.

A moderação excecional de conteúdo privado segue a mesma fronteira. O
superuser não entra no tenant, não usa handlers funcionais do trainer e não
recebe CRUD geral sobre dados privados. Casos administrativos dedicados podem
bloquear ou desbloquear um `Food` ou `Exercise` privado quando existir conteúdo
malicioso, perigoso, ofensivo ou deliberadamente fraudulento. Cada operação
exige role `superuser`, `UserId` autenticado, `IsAdministrative`, motivo
estruturado e auditoria append-only na mesma transação.

O acesso administrativo ignora filtros de tenant apenas dentro do store
dedicado e apenas para o recurso identificado. Não existe listagem transversal
de conteúdo privado como efeito implícito da role. As operações de moderação
voltam a validar no PostgreSQL o estado atual do superuser, conforme a secção
5.3, e têm testes negativos sem contexto administrativo.

PostgreSQL Row-Level Security não faz parte do MVP. Deve ser reavaliado quando:

1. Existirem requisitos de compliance.
2. A equipa conseguir operar correctamente session context com connection pooling.
3. Jobs e administração tiverem uma estratégia de contexto comprovada.
4. Os testes demonstrarem que a complexidade adicional é sustentável.

A decisão está registada no Sprint 9B. O sprint representa uma reavaliação
arquitectural e não uma implementação automática de RLS.

## 7. Persistência e migrations

### 7.1 PostgreSQL

Neon PostgreSQL é a fonte de verdade para:

1. Dados de domínio.
2. Identidade e refresh tokens.
3. Estado de billing.
4. Eventos Stripe processados.
5. Jobs duráveis.
6. Outbox.

Redis, QStash e Stripe não substituem o estado autoritativo local.

### 7.2 EF Core

A Infrastructure usa EF Core 10 com o provider:

```text
Npgsql.EntityFrameworkCore.PostgreSQL
```

Lazy loading fica desactivado. Queries de leitura devem preferir:

1. Projecção para o resultado necessário.
2. `AsNoTracking` quando não existe alteração.
3. Includes explícitos apenas quando necessários.
4. Paginação para colecções potencialmente grandes.

### 7.3 Estratégia de migrations

Não existem dados de produção a preservar. Depois de `01_DATABASE_SCHEMA.md` ser aprovado:

1. O modelo EF Core é criado a partir do schema validado.
2. É gerada uma migration nova `InitialCreate`.
3. O histórico de migrations Python não é convertido.
4. As migrations futuras são imutáveis depois de aplicadas num ambiente partilhado.
5. Correcções usam uma migration nova.

A API não executa `Database.Migrate()` automaticamente no arranque.

No plano gratuito do Render, que não disponibiliza pre-deploy command, a migration bundle deve ser executada como passo de release controlado a partir de um ambiente confiável. Quando o serviço for pago, pode ser adoptado um pre-deploy step dedicado.

Migrations futuras devem seguir expand-contract quando uma alteração precisar de compatibilidade entre versões durante o deploy.

## 8. Cache e rate limiting

### 8.1 HybridCache e Upstash Redis

O Sprint 6B contém um gate obrigatório para decidir se o MVP necessita de cache ou
rate limiting distribuídos. Só quando as métricas do Sprint 6A e um consumidor
concreto justificarem essa decisão é que a Application define uma porta estreita
para o caso de uso e a Infrastructure a pode implementar sobre HybridCache:

1. Cache local em memória como camada primária.
2. Upstash Redis como camada distribuída.
3. Protecção contra cache stampede.
4. TTL explícito.
5. Invalidação documentada por feature.

Redis é usado apenas para dados reconstruíveis. Não guarda:

1. Fonte autoritativa de subscrições.
2. Ownership de recursos.
3. Refresh tokens autoritativos.
4. Estado exclusivo de jobs.
5. Decisões de autorização que não possam ser novamente validadas.

### 8.2 Política de falha

Falhas de Redis não podem impedir operações principais. A aplicação faz fallback para a fonte de verdade ou para cache local.

O rate limiting geral é fail-open para preservar disponibilidade. Endpoints sensíveis, como login e recuperação de conta, usam fallback local quando Redis está indisponível.

Devem existir:

1. Timeouts curtos.
2. Retry limitado apenas para falhas transitórias.
3. Métricas de hit, miss, erro e latência.
4. Prefixos separados por ambiente.

### 8.3 Implementação concreta do rate limiting

`Microsoft.AspNetCore.RateLimiting`, nativo do ASP.NET Core desde o .NET 7, sem package externo adicional. Endpoints sensíveis (login, signup, recuperação de conta) usam uma política dedicada mais restritiva que a política geral da API.

## 9. Jobs duráveis e QStash

### 9.1 Decisão do MVP

RabbitMQ e MassTransit não fazem parte do MVP.

O plano gratuito do Render suspende a API depois de um período sem tráfego e não disponibiliza background workers gratuitos. Um `BackgroundService`, Quartz ou Hangfire alojado apenas no processo da API não executaria de forma fiável durante a suspensão.

No plano gratuito, QStash chama um endpoint interno da API a cada vinte minutos. Esta chamada acorda a aplicação e activa o dispatcher.

Uma frequência inferior a quinze minutos impediria o Render de suspender. Uma query ao Neon em intervalos inferiores ao seu período de inactividade também poderia impedir scale-to-zero e exceder as 100 CU-hours gratuitas. A frequência de vinte minutos preserva margem para suspensão dos dois serviços e deve ser confirmada através das métricas reais de utilização.

O intervalo é configurável. Só pode ser reduzido depois de existir orçamento de compute ou de a aplicação migrar para planos que suportem execução contínua.

### 9.2 Endpoint interno

O endpoint conceptual é:

```text
POST /api/internal/jobs/dispatch
```

Este endpoint:

1. Não usa autenticação de utilizador.
2. Exige validação da assinatura QStash.
3. Limita o tamanho do body.
4. Processa um batch limitado.
5. Propaga correlation ID.
6. Não expõe detalhes dos jobs na resposta.

### 9.3 Estado de um job

Um job durável contém, no mínimo:

1. Identificador.
2. Trainer, quando aplicável.
3. Tipo e versão.
4. Payload.
5. Data de execução em UTC.
6. Estado.
7. Número de tentativas.
8. Próxima tentativa.
9. Idempotency key.
10. Correlation ID.
11. Erro sanitizado da última tentativa.
12. Expiração do lease de processamento.
13. Identificador do owner da lease.

Estados conceptuais:

1. Pending
2. Processing
3. Completed
4. Failed
5. DeadLetter

A nomenclatura e o schema finais pertencem à fase de database schema.

### 9.4 Processamento

O dispatcher:

1. Reclama jobs vencidos de forma transaccional.
2. Atribui um lease com owner e duração limitada e evita que duas execuções processem o mesmo job em paralelo.
3. Cria um scope e TenantContext por job.
4. Chama o handler da Application.
5. Regista sucesso ou calcula a próxima tentativa.
6. Move falhas permanentes para estado terminal.

A entrega é tratada como at-least-once. Handlers devem ser idempotentes.

Um job em `Processing` cujo lease expire volta a ser elegível. Processamento demorado renova o lease antes da expiração. Esta recuperação evita que uma interrupção do processo deixe o job bloqueado permanentemente.

O cold start do Render e o intervalo de vinte minutos significam que um lembrete pode sofrer atraso superior a vinte minutos em situações degradadas. Este compromisso é aceite apenas para o MVP gratuito.

### 9.5 Critérios para RabbitMQ

RabbitMQ só volta a ser avaliado quando existir pelo menos um destes sinais:

1. Múltiplos consumers independentes.
2. Necessidade de escalar consumers separadamente da API.
3. Throughput incompatível com polling PostgreSQL.
4. Latência exigida inferior ao intervalo do dispatcher.
5. Integração entre processos ou serviços distintos.
6. Necessidade operacional comprovada de routing ou fan-out.

Se for adoptado, a decisão deve incluir broker gerido, worker separado, transactional outbox, inbox, idempotência, retries, delayed redelivery, dead-letter queues, observabilidade e custo. A adopção de MassTransit exige ainda uma avaliação actualizada do licenciamento.

A reavaliação está registada no Sprint 9B. A presença no roadmap não substitui os
critérios anteriores nem aprova antecipadamente um broker.

## 10. Stripe

Stripe gere exclusivamente a subscrição SaaS do trainer ao PT Manager. Os packs
de sessões vendidos ou atribuídos aos clientes são domínio interno do trainer,
mantêm snapshots comerciais locais e não possuem customer, price, payment intent
ou subscription IDs da Stripe.

### 10.1 Operações iniciadas pela aplicação

Checkout e Customer Portal são criados através das portas específicas
`ICheckoutGateway` e `ICustomerPortalGateway`. A reconciliação de subscrições usa
`ISubscriptionReconciliationGateway`. Esta separação preserva contratos estreitos e
corresponde aos consumidores reais da Application.

Todos os pedidos mutáveis enviados à Stripe usam uma idempotency key estável por operação de negócio. Retries usam a mesma key e os mesmos parâmetros.

Chamadas à Stripe:

1. Têm timeout.
2. Tratam rate limiting e erros transitórios.
3. Não mantêm uma transacção PostgreSQL aberta durante a chamada externa.
4. Registam o Stripe request ID sem guardar dados sensíveis.

### 10.2 Webhook

O webhook Stripe:

1. Lê o raw body sem o modificar.
2. Valida `Stripe-Signature` com o secret do ambiente.
3. Processa apenas event types explicitamente suportados.
4. Deduplica através de `event.id`.
5. Resolve o trainer através de relações persistidas.
6. Actualiza o estado local numa transacção.
7. Regista efeitos secundários na outbox na mesma transacção.
8. Devolve 2xx apenas depois do commit durável.
9. Devolve uma resposta de erro quando a persistência falha, permitindo retry da Stripe.

O endpoint Stripe é configurado para uma versão explícita da Stripe API. Um evento autenticado mas não suportado é registado de forma sanitizada e recebe 2xx, evitando retries inúteis. A configuração da Stripe deve subscrever apenas os event types necessários.

Eventos Stripe podem chegar duplicados ou fora de ordem. Quando a ordem afectar a decisão, o handler reconcilia o estado actual com a Stripe em vez de confiar apenas na sequência de entrega.

### 10.3 Outbox

A outbox liga alterações PostgreSQL a efeitos secundários sem uma transacção distribuída.

Exemplos:

1. Enviar email após pagamento confirmado.
2. Avisar sobre falha de pagamento.
3. Actualizar notificações internas.

O dispatcher de jobs entrega os itens da outbox de forma idempotente. Um item só é concluído depois de o efeito correspondente ser confirmado.

## 11. Serviços externos

As integrações pertencem à Infrastructure e são acedidas através de portas da Application:

| Capacidade | Porta | Implementação inicial |
|---|---|---|
| Email de autenticação | `IAuthenticationEmailSender` | Resend |
| Entrega de notificações | Porta específica criada com o dispatcher | Resend |
| Checkout | `ICheckoutGateway` | Stripe |
| Customer Portal | `ICustomerPortalGateway` | Stripe |
| Reconciliação de subscrições | `ISubscriptionReconciliationGateway` | Stripe |
| Media | `IMediaStorage` | Cloudinary |
| Moderação de avatar | `IImageModerationService` | Fornecedor escolhido no Sprint 5C |
| Cache | Porta criada apenas com o primeiro consumidor real | HybridCache e Upstash Redis |
| Activação de jobs | Endpoint interno | Upstash QStash |

No Sprint 0 são confirmados apenas os packages necessários ao scaffold, aos projectos de teste vazios e ao OpenAPI criado pelo template. Os packages e SDKs de integrações futuras são avaliados no sprint do primeiro consumidor, para validar manutenção, compatibilidade e versão no momento em que passam a ser usados. Quando um SDK não estiver activamente mantido, deve ser preferido um `HttpClient` tipado sobre uma dependência desactualizada.

## 12. Observabilidade

### 12.1 Logs

A aplicação usa `ILogger` e logs estruturados.

Em produção:

1. Output JSON para console.
2. Correlation ID em cada pedido.
3. Trace ID associado aos logs.
4. Event IDs estáveis para eventos relevantes.
5. Redaction de passwords, tokens, cookies, API keys e dados pessoais desnecessários.

Não existe file sink no Render. O filesystem do container é efémero.

### 12.2 Erros, traces e métricas

Sentry recebe erros não tratados e contexto sanitizado.

OpenTelemetry instrumenta:

1. ASP.NET Core.
2. HttpClient.
3. EF Core e Npgsql.
4. Runtime .NET.
5. Jobs e integrações externas através de Activities próprias.

O exporter e a retenção são confirmados no Sprint 0 de acordo com os limites gratuitos disponíveis.

Métricas mínimas:

1. Latência e erros HTTP.
2. Duração e falhas de queries.
3. Pool de ligações.
4. Cache hit ratio e falhas Redis, apenas se a implementação for aprovada no
   Gate 6B; até lá, métricas de latência e volume suportam essa decisão.
5. Jobs pendentes, tentativas e DeadLetter.
6. Webhooks Stripe duplicados e falhados.
7. Falhas de email.

### 12.3 Health checks

Existem endpoints separados:

```text
GET /health/live
GET /health/ready
```

`/health/live` confirma apenas que o processo responde. Não consulta serviços externos.

`/health/ready` confirma que a aplicação consegue usar PostgreSQL. Redis, QStash, Stripe, Resend e Cloudinary não bloqueiam readiness, porque não são necessários para responder a todos os pedidos.

Os endpoints devolvem apenas estado agregado. Detalhes internos ficam nos logs e na telemetria.

## 13. Deploy

### 13.1 Topologia inicial

```text
Vercel
    Frontend React
        |
        v
Render Free Web Service
        .Api
        |
        |---- Neon PostgreSQL
        |---- Upstash Redis (condicional ao Gate 6B)
        |---- Stripe
        |---- Resend
        `---- Cloudinary

Upstash QStash
    Chamada assinada a cada vinte minutos
        |
        v
Render Free Web Service
    /api/internal/jobs/dispatch
```

Não existe Worker Service no MVP.

### 13.2 Limitações aceites

O plano gratuito do Render:

1. Suspende a API após inactividade.
2. Pode introduzir cold start significativo.
3. Usa filesystem efémero.
4. Não permite múltiplas instâncias.
5. Não disponibiliza background workers gratuitos.
6. Não disponibiliza pre-deploy command.

Estas limitações tornam a topologia adequada a desenvolvimento, demonstração e validação do MVP. Não constituem uma garantia de disponibilidade para produção comercial.

### 13.3 Container

O Dockerfile final deve:

1. Restaurar projectos antes de copiar todo o source para preservar cache.
2. Publicar explicitamente `Api`.
3. Usar imagem runtime.
4. Executar como utilizador não root.
5. Escutar a porta fornecida pelo Render.
6. Propagar shutdown e `CancellationToken`.
7. Excluir `backend-python/`, secrets, testes e artefactos locais do build context.

## 14. Baseline tecnológico

O Central Package Management é criado no Sprint 0. Cada versão exacta é adicionada a `Directory.Packages.props` quando existe um `PackageReference` consumidor. Packages de sprints futuros não são instalados antecipadamente.

| Área | Decisão |
|---|---|
| Runtime | .NET 10 LTS |
| Linguagem | C# compatível com .NET 10 |
| HTTP | ASP.NET Core Controllers |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` |
| Persistência | EF Core 10 |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Identidade | ASP.NET Core Identity |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Validação | FluentValidation core com validação explícita assíncrona |
| Cache | Cache local no MVP; HybridCache e provider Redis compatível condicionais ao Gate 6B |
| Observabilidade | `ILogger`, Sentry e OpenTelemetry |
| Testes | xUnit, WebApplicationFactory e Testcontainers |
| Cobertura | Coverlet ou Microsoft Code Coverage |

Não fazem parte do baseline:

1. `Microsoft.EntityFrameworkCore.Npgsql`
2. `FluentValidation.AspNetCore`
3. `MassTransit.AspNetCore`
4. MassTransit e RabbitMQ
5. AutoMapper
6. MediatR
7. OpenCover
8. Serilog file sink

Packages directos usam lock file e actualizações automáticas controladas. Uma major version não é actualizada sem leitura das breaking changes e execução da suite completa.

## 15. Estratégia de testes

### 15.1 Unit tests

Domain Unit Tests validam invariantes e value objects sem mocks.

Application Unit Tests validam handlers com doubles das portas externas.

### 15.2 Integration e functional tests

Infrastructure Integration Tests usam PostgreSQL real através de Testcontainers.

API Functional Tests usam WebApplicationFactory e validam:

1. Rotas.
2. Serialização `snake_case`.
3. Autenticação.
4. Autorização.
5. Problem Details.
6. Compatibilidade dos contratos.

Se o Gate 6B aprovar Redis, o adapter é testado com um container nos cenários
específicos de cache. Os testes principais devem provar que a aplicação continua
funcional quando Redis está indisponível.

### 15.3 Testes multi-tenant

A suite inclui, no mínimo:

1. Trainer não lê dados de outro trainer.
2. Trainer não actualiza nem elimina dados de outro trainer.
3. IDs manipulados no body não alteram o tenant.
4. Escritas com tenant adulterado são rejeitadas.
5. Catálogos globais e privados respeitam a política definida.
6. Client acede apenas ao próprio agregado.
7. Job executa apenas no tenant persistido.
8. Superuser só faz bypass através de caso de uso administrativo.
9. Cache keys não colidem entre tenants.

### 15.4 Testes de autenticação

A suite cobre:

1. Login válido e inválido.
2. Utilizador suspenso.
3. Email não verificado.
4. Refresh com rotação.
5. Reutilização de refresh token.
6. Revogação no logout.
7. CORS e envio do cookie.
8. Validação de Origin e token anti-CSRF.
9. Rejeição de refresh e logout iniciados por origem não autorizada.
10. Expiração e janela residual máxima do access token.

### 15.5 Testes Stripe

A suite cobre:

1. Assinatura válida e inválida.
2. Raw body alterado.
3. Event type desconhecido autenticado devolve 2xx sem efeito.
4. Evento duplicado.
5. Eventos fora de ordem.
6. Falha antes do commit.
7. Falha depois de criar a outbox.
8. Retry de chamada Stripe com a mesma idempotency key.

### 15.6 Testes de jobs

A suite cobre:

1. Assinatura QStash inválida.
2. Job ainda não vencido.
3. Reclamação concorrente.
4. Retry transitório.
5. Falha permanente.
6. Idempotência.
7. Tenant inexistente ou suspenso.
8. Recuperação de job preso em Processing.
9. Transição para DeadLetter.
10. Owner e expiração do lease.
11. Renovação de lease em processamento demorado.

### 15.7 Architecture tests

Testes automáticos impedem:

1. Domain depender de Application, Infrastructure ou API.
2. Application depender de Infrastructure ou API.
3. Controllers usarem directamente o DbContext.
4. Infrastructure expor implementações concretas fora do composition root.
5. Features funcionais usarem `IgnoreQueryFilters` sem autorização explícita.

## 16. Critérios de aceitação da arquitectura

A arquitectura está pronta para orientar implementação quando:

1. O diagrama de dependências corresponde às referências reais dos projectos.
2. O contrato `/api/v1` tem matriz Preserve, Alias ou Remove.
3. O schema multi-tenant suporta as constraints definidas nesta arquitectura.
4. Não existe qualquer instrução para converter migrations Python.
5. Redis é sempre tratado como cache reconstruível.
6. PostgreSQL é a fonte de verdade para jobs e outbox.
7. QStash é apenas o activador autenticado do dispatcher.
8. Stripe tem assinatura, deduplicação, idempotência e reconciliação.
9. Health checks distinguem liveness de readiness.
10. O deploy gratuito não é descrito como produção altamente disponível.
11. Dependências e versões exactas são validadas no Sprint 0.
12. As afirmações de segurança são demonstráveis por testes.
13. A frequência QStash respeita os orçamentos medidos de Render, Neon e QStash.

## 17. Registo de decisões diferidas

Uma capacidade retirada de um sprint continua registada com origem, destino,
estado e critério de entrada na tabela central de
`.claude/project/02_SPRINTS_ROADMAP.md`. Um sprint futuro não constitui aprovação
automática para implementar uma capacidade condicional.

As decisões anteriormente listadas nesta secção têm o seguinte estado:

1. Schema final, configurações EF Core, índices, constraints e `InitialCreate`
   foram materializados nos sprints de persistência e deixaram de estar adiados.
2. Roadmap, `AGENTS.md` e instruções operacionais foram actualizados durante a
   preparação e execução dos sprints atuais.
3. A configuração final de CI/CD está agendada no Sprint 7.
4. HybridCache e Upstash Redis passam pelo Gate 6B e permanecem registados no
   Sprint 9B se a implementação não for justificada nesse gate.
5. Revisão humana de avatares, moderação automática de vídeo, antivírus e scanning
   adicional e uma fila de denúncias/evidência estão agendados para avaliação no
   Sprint 9A.
6. PostgreSQL RLS, RabbitMQ/MassTransit, eventual extração de microserviços e
   revogação global imediata de access tokens estão agendados para reavaliação no
   Sprint 9B.
7. Métricas customizáveis por cliente (`client_tracked_metrics` e
   `client_metric_values`), versionamento de planos de treino e nutrição,
   relatórios persistidos (`client_reports`), `client_consents` e consulta
   administrativa read-only de trainers estão registados no Sprint 9C.
8. Registo de séries e cancelamento de sessões pelo próprio cliente estão
   registados no Sprint 9C. A consolidação de `StartDate` e `StartsDate` está
   registada no Sprint 9D, sujeita à matriz Preserve, Alias ou Remove.

AutoMapper, MediatR, repositório genérico e Unit of Work genérico não são itens
diferidos. São opções arquiteturais rejeitadas para este projeto enquanto não
existir uma decisão explícita que altere a arquitetura aprovada.

## 17.1 Decisões de schema anteriores à InitialCreate

1. Um `Client` nasce sem `User`. O convite referencia inequivocamente o
   `ClientId` e a conta só pode ser associada uma vez depois da aceitação.
2. Uma `Session` é marcada por `StartsAt` em UTC, duração, localização opcional,
   pack opcional e um estado explícito. As transições inválidas são rejeitadas
   no Domain.
3. `ClientSessionPack` guarda o snapshot de nome, quantidade de sessões, preço e
   moeda. Alterar ou desativar o catálogo não reescreve uma venda anterior.
4. Um cliente pode ter múltiplos planos alimentares. `KcalTarget` é uma meta
   explícita e independente das calorias calculadas a partir das macros.
5. Existe no máximo um plano de treino ativo por cliente. A troca de plano ativo
   é transacional e protegida por índice único parcial.
6. Suplementos podem ser associados a refeições ou atribuídos diretamente a um
   cliente através de `ClientSupplementAssignment`.
7. `client_consents` não integra a `InitialCreate`. Qualquer necessidade legal
   futura exige análise própria e não deve ser inferida a partir de dados de
   avaliação. A avaliação está registada no Sprint 9C.

## 17.2 Decisões nutricionais anteriores à InitialCreate

O cálculo nutricional é um serviço puro do Domain. `Client` e
`InitialAssessment` fornecem apenas sugestões editáveis. `CheckIn` nunca
recalcula um `MealPlan`.

Cada `MealPlan` guarda alvos relacionais e um `calculation_snapshot` JSONB
obrigatório. O agregado deriva os alvos relacionais do snapshot para impedir
divergências.

Fórmulas de energia suportadas:

1. Harris-Benedict revista de 1984.
2. Mifflin-St Jeor de 1990.
3. Cunningham de 1980, com massa livre de gordura derivada do peso e da
   percentagem de gordura.
4. Tinsley por peso corporal de 2018.

Modos de distribuição de macronutrientes:

1. `percentage`, com total exato de 100,00%.
2. `grams_per_kg`, com proteína e gordura explícitas e hidratos residuais.
3. `manual_grams`, com diferença energética absoluta máxima de 100 kcal.

`ClientSupplementAssignment` pertence ao tenant. As leituras usam Global Query
Filter, as escritas validam `OwnerTrainerId` e a FK composta garante que o
`Client` pertence ao mesmo tenant. A referência a `Supplement` aceita itens do
catálogo global ou privados do trainer efetivo e rejeita itens arquivados para
novas referências ou privados de outro tenant.

`Supplement` e `ClientSupplementAssignment` não usam soft delete. `IsActive`
representa disponibilidade e preservação histórica. Arquivar um suplemento não
altera meal plans nem atribuições existentes; uma atribuição ativa continua
legível pelo cliente, incluindo quando o cliente foi arquivado. Um trainer gere
apenas suplementos privados próprios e nunca vê os suplementos privados de outro
trainer.

A administração global de suplementos usa casos de uso dedicados e autorização
fail-closed: role `superuser`, `UserId` autenticado e `IsAdministrative`. Create,
Update, Archive, Reactivate e Delete gravam uma `AdministrativeAuditEntry`
append-only na mesma transação PostgreSQL. A auditoria guarda ator, ação, tipo e
ID do recurso, instante e snapshots JSONB mínimos. Não existe FK da auditoria
para `Supplement`, para que o histórico sobreviva ao hard delete.

O hard delete de um suplemento global só é permitido sem referências em
`MealPlanMealSupplement` e `ClientSupplementAssignment`. A verificação usa um
único `EXISTS` sobre a união das dependências e as FKs `RESTRICT` fecham a corrida
com novas associações. Leituras de cliente juntam `Client`, atribuição e
`Supplement` no mesmo SQL e nunca projetam `Supplement.TrainerNotes`.

### 17.3 Media de imagem gerida

#### 17.3.1 Branding e logótipo padrão

`TrainerSettings.LogoUrl` e `LogoPublicId` representam exclusivamente media
personalizado do trainer. Ambos são nullable. Null significa que não existe
logótipo personalizado e que o frontend deve apresentar o asset padrão do PT
Manager incluído na própria aplicação.

O backend não conhece a localização desse asset, não devolve uma URL global e
não cria um registo Cloudinary para ele. ReplaceLogo guarda a URL e o public ID
personalizados. RemoveLogo limpa ambos e agenda apenas a eliminação do media
anterior. As leituras de settings do trainer e branding do cliente projetam
`LogoUrl` sem `COALESCE`; uma DTO válida com `LogoUrl = null` é sucesso.

#### 17.3.2 Avatar do cliente e moderação síncrona

O avatar é media gerido pela aplicação e representa a fotografia de perfil do
cliente no portal. Apenas o próprio cliente autenticado pode substituir ou remover
o avatar. O trainer, o superuser e callers que forneçam `client_id` não recebem esta
capacidade. A identidade do cliente e o tenant efectivo vêm exclusivamente de
`ITenantContext`.

O contrato HTTP não aceita uma URL arbitrária.
`PUT /api/v1/portal/my-profile/avatar` recebe a imagem em `multipart/form-data` e
`DELETE /api/v1/portal/my-profile/avatar` remove o avatar personalizado. Ambos são marcados
com `SensitiveResponse`, que apenas aplica `Cache-Control: no-store` e
`Pragma: no-cache`; este atributo não classifica conteúdo nem representa o resultado da
moderação.

O Sprint 5C implementa moderação síncrona e fail-closed. Antes de publicar a imagem,
o fluxo valida tamanho, MIME declarado, assinatura real, descodificação, dimensões e
número máximo de píxeis. O nome original nunca controla o identificador ou o path no
storage. A Application usa uma porta `IImageModerationService`, independente do
fornecedor, cujo resultado representa `Approved`, `Rejected`, `ReviewRequired` ou
`Unavailable`.

Só `Approved` permite publicar e persistir o novo avatar. No MVP,
`ReviewRequired` é rejeitado sem revisão humana e `Unavailable` é uma falha
temporária fail-closed. A resposta usa Problem Details com código estável e detalhe
seguro; não expõe categorias internas, scores ou thresholds do classificador. O
avatar anterior permanece activo em qualquer falha.

Sempre que o fornecedor de moderação aceitar o conteúdo directamente, a moderação
ocorre antes do upload público. Se o fornecedor exigir uma URL, o asset fica em
quarentena privada e sem URL pública até à aprovação. Conteúdo rejeitado é eliminado.
Uma falha depois do upload tenta compensar eliminando o novo asset; depois do commit,
a eliminação do asset anterior é entregue pela outbox.

Na selecção do fornecedor, prefere-se moderação directa do conteúdo para manter o
asset rejeitado fora do storage. Também são avaliados retenção de dados, região de
processamento, contrato de privacidade, latência, disponibilidade, custo e qualidade
dos falsos positivos em fotografias de fitness. Se for necessária quarentena, essa
capacidade entra numa porta explícita; não se alarga `IMediaStorage` com opções
genéricas sem um consumidor concreto.

`Client.AvatarUrl` e `Client.AvatarPublicId` são nullable e formam um par: ambos null
significam que o frontend apresenta o avatar padrão; caso contrário, ambos têm valor.
Não se persiste estado de moderação, porque o fluxo aprovado é síncrono e nenhuma
imagem pendente se torna avatar activo. A introdução de `AvatarPublicId` exige uma
migration EF Core nova, gerada no Sprint 5C, e uma constraint que impeça pares
parcialmente preenchidos.

Duas substituições concorrentes do mesmo avatar são serializadas sobre a ficha do
cliente. A transacção que persiste a referência volta a confirmar o avatar activo e
nunca agenda a eliminação de um public ID que continue referenciado. O upload tem
rate limiting próprio para limitar abuso e custos de storage e moderação.

### 17.4 Vídeos de exercícios e moderação futura

Até ao fim do Lote 3E, `Exercise.VideoUrl` continua a representar apenas uma
URL HTTPS externa. O backend valida formato absoluto e comprimento, mas não
descarrega o recurso, não o classifica como tecnicamente verificado e não
garante disponibilidade, integridade ou adequação do conteúdo. Uma URL externa
nunca é equivalente a um upload gerido pela aplicação.

O upload gerido de vídeo fica planeado como vertical slice do Sprint 5D, depois
da migration consolidada do Lote 3F, da autenticação e dos contratos HTTP do
Sprint 4 e da integração base do Cloudinary no Sprint 5C. Os limites concretos
de tamanho, duração, resolução, codecs e quotas são decisões de produto a
fechar antes desse slice; não são antecipados no schema nem no código atual.

O desenho futuro deve:

1. Autorizar o trainer e confirmar o ownership do exercício sem aceitar
   `trainer_id` do cliente.
2. Usar storage privado, identificadores gerados pelo servidor e acesso por URL
   assinada de curta duração.
3. Preferir upload direto do browser para o storage através de autorização
   limitada emitida pelo backend; a finalização confirma no fornecedor o asset,
   o tamanho real e a integridade antes de o considerar processável.
4. Validar extensão permitida, MIME declarado e estrutura real do container,
   incluindo codec, duração e resolução. Nenhum destes sinais é suficiente
   isoladamente.
5. Processar o vídeo de forma assíncrona através dos jobs duráveis existentes,
   com timeouts, limites de recursos, retry apenas para falhas transitórias e
   limpeza de uploads abandonados.
6. Aplicar rate limiting por ator e quotas de negócio persistidas em PostgreSQL;
   Redis nunca é a fonte de verdade de limites associados ao plano comercial.
7. Representar o lifecycle técnico com `Pending`, `Processing`, `Ready`,
   `Rejected` e `Failed`. `Ready` significa apenas validação técnica concluída.
8. Manter o nome original fora do identificador e do path do storage. Se for
   conservado como metadata, é tratado como input não confiável.
9. Manter assets em quarentena privada até à validação. Antivírus e scanning
   adicional são avaliados no Sprint 9A quando a infraestrutura e o risco o
   justificarem.

A moderação automática destes vídeos para nudez, conteúdo sexual, violência, armas
ou relevância para fitness não pertence ao Sprint 5D nem ao MVP atual. A moderação
síncrona do avatar é a excepção explícita definida na secção 17.3.2. Quando
existir política de conteúdo, processo de revisão humana, orçamento e fornecedor
escolhido, a decisão é reavaliada no Sprint 9A. Se aprovada, a Application define
uma porta específica de moderação e a
Infrastructure fornece o adaptador concreto. O Domain e a lógica de negócio não
dependem de Google, AWS, Azure ou outro fornecedor.

O resultado de moderação é uma dimensão separada do estado técnico e deve poder
representar `Approved`, `Rejected` e `ReviewRequired`. A interface, semelhante a
`IVideoModerationService`, só é criada com o primeiro consumidor real, porque os
fornecedores podem exigir contratos síncronos, jobs assíncronos ou callbacks
diferentes. Não são criados agora serviços fictícios, SDKs de AI ou chamadas
externas apenas para antecipar essa integração.

### 17.5 Moderação administrativa de catálogos privados

`IsActive` representa exclusivamente a decisão do proprietário sobre a
disponibilidade do seu `Food` ou `Exercise`. Uma decisão da plataforma usa um
estado independente, `PlatformEnforcementStatus`, com `Allowed` e `Blocked`.
O trainer não pode escrever esse estado nem remover um bloqueio administrativo.

O superuser pode bloquear e desbloquear, mas não editar os campos funcionais,
trocar o owner ou executar hard delete de conteúdo privado por este fluxo. Um
erro plausivelmente acidental deve originar pedido de correção; o bloqueio fica
reservado a casos restritos de maldade, perigo, fraude deliberada ou violação
material da política da plataforma. Suspender o utilizador por reincidência é
uma decisão de segurança separada do estado de cada recurso.

O motivo usa uma allowlist inicial de códigos estáveis:
`malicious_content`, `dangerous_information`,
`deliberately_false_information` e `prohibited_content`. Texto livre interno
não substitui o código e não é exposto nos DTOs funcionais.

O bloqueio tem efeito imediato:

1. Novas referências ao recurso são rejeitadas.
2. O conteúdo bloqueado deixa de ser apresentado no portal do cliente.
3. As referências existentes permanecem persistidas para integridade e
   investigação, sem expor ao cliente o conteúdo proibido.
4. Um `MealPlan` ou `TrainingPlan` que referencie o recurso é identificado
   como necessitando de revisão. Esta condição deriva das referências atuais e
   do estado do recurso; não é persistida como booleano duplicado no plano.
5. O trainer recebe apenas o motivo adequado para corrigir o plano; notas
   internas de investigação não são expostas.

O contrato HTTP exato para representar conteúdo indisponível no portal é
fechado no desenho do Sprint 4B e classificado pela matriz Preserve, Alias ou
Remove antes de implementar o controller. O contrato nunca pode devolver os
campos bloqueados ao cliente apenas para preservar a forma antiga do payload.

Block e Unblock são idempotentes para o mesmo estado pedido. Unblock não altera
`IsActive`; se o trainer tiver arquivado o recurso, este continua arquivado. A
escrita do estado e da `AdministrativeAuditEntry` é atómica. Uma falha de
auditoria impede a mudança de estado.

A primeira implementação pertence a um vertical slice Sprint 4B, depois da
autenticação e das políticas administrativas do Sprint 4A. Inclui uma migration
EF Core nova gerada a partir do modelo desse slice; não altera migrations
aplicadas nem aumenta o âmbito da migration consolidada do Lote 3F. Um sistema
genérico de denúncias, evidência e filas de revisão fica registado no Sprint 9A e
só entra em implementação quando existir um caso real que o justifique.

## 17.6 Google Sign-In

Estado: implementado no backend real (2026-09-06). Migration aplicada à base local.

A Application define contratos provider-neutral para emitir e consumir challenges,
validar identidades externas e executar sign-in ou linking. Apenas Infrastructure
referencia `Google.Apis.Auth`. A Api limita-se ao contrato HTTP, ao cookie do nonce,
às policies e à conversão de `Result` em Problem Details.

O identificador externo é sempre `(provider, subject)`. O email nunca identifica a
conta Google nem cria linking automático. Linking exige utilizador PT Manager
autenticado, password local atual, challenge associado ao próprio UserId e email
Google coincidente com o email principal.

O nonce tem pelo menos 256 bits, é devolvido ao frontend uma vez, persiste apenas como
hash e é consumido atomicamente no PostgreSQL. O cookie
`__Secure-ptm-google-nonce` é HttpOnly, Secure, host-only, dura cinco minutos e usa
`Path=/api/v1/auth/google` e o SameSite dos cookies Auth.

Uma identidade nova sem convite cria trainer e trial. Gmail ou Workspace autoritativo
recebe sessão; outro domínio recebe 202 e confirmação PT Manager. Uma identidade nova
com convite válido cria client, associa o Client do convite e consome o convite na mesma
transação. Uma identidade conhecida ignora o convite apresentado no sign-in.

## 18. Referências oficiais

1. [.NET releases and support](https://learn.microsoft.com/dotnet/core/releases-and-support)
2. [EF Core global query filters](https://learn.microsoft.com/ef/core/querying/filters)
3. [EF Core multi-tenancy](https://learn.microsoft.com/ef/core/miscellaneous/multitenancy)
4. [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
5. [ASP.NET Core Identity for SPA backends](https://learn.microsoft.com/aspnet/core/security/authentication/identity-api-authorization)
6. [ASP.NET Core OpenAPI](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview)
7. [ASP.NET Core HybridCache](https://learn.microsoft.com/aspnet/core/performance/caching/hybrid)
8. [ASP.NET Core health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
9. [FluentValidation ASP.NET Core guidance](https://docs.fluentvalidation.net/en/latest/aspnet.html)
10. [Stripe webhooks](https://docs.stripe.com/webhooks)
11. [Stripe idempotent requests](https://docs.stripe.com/api/idempotent_requests)
12. [Upstash QStash schedules](https://upstash.com/docs/qstash/features/schedules)
13. [Upstash QStash security](https://upstash.com/docs/qstash/features/security)
14. [Render free instance limitations](https://render.com/docs/free)
15. [Google ID token validation](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token)
