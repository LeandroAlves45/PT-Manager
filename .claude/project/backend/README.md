# PT Manager — Backend (índice rápido para consumo)

Resumo operacional do backend .NET 10 para quem o consome (frontend, agentes, testes).
A arquitetura completa continua em `../00_ARCHITECTURE.md`, o schema em
`../01_DATABASE_SCHEMA.md`, o roadmap em `../02_SPRINTS_ROADMAP.md` e o setup em
`../03_DEVELOPER_GUIDE.md`. Esta pasta **não substitui** esses documentos; organiza o que
o frontend precisa.

## Documentos

| Documento | Conteúdo |
|---|---|
| [01_API_ENDPOINTS.md](01_API_ENDPOINTS.md) | 142 endpoints v1 por grupo, com role, parâmetros e rate limit |
| [02_CONTRATO_HTTP_FRONTEND.md](02_CONTRATO_HTTP_FRONTEND.md) | Auth (cookie + CSRF), CORS, erros, paginação, rate limits, uploads, flags |

## Estado (2026-09-15)

- Sprints 0–4 fechados; Sprint 5: 5A, 5B e 5C fechadas (5B aguarda secrets Stripe; 5C
  aguarda secrets Cloudinary/Vision); **5D em implementação**.
- Sprint 6 (Frontend) só arranca depois do Gate 5D.
- Estado operacional detalhado: `.claude/memory/ACTIVE.md` e `.claude/memory/MEMORY.md`.

## Pontos fundamentais

1. **Arquitetura:** modular monolith, Clean Architecture (Domain → Application →
   Infrastructure → Api). Sem MediatR, AutoMapper, repositório ou Unit of Work genéricos.
2. **Multi-tenancy:** tenant só a partir de `ITenantContext`; query filters, interceptor de
   escrita e testes cross-tenant. O frontend nunca envia o tenant: vem do token.
3. **Roles:** `superuser` (catálogos globais, moderação), `trainer` (tenant), `client`
   (portal do próprio cliente).
4. **Auth:** JWT de 15 min em memória + refresh HttpOnly de 30 dias com rotação e CSRF.
5. **Contrato:** JSON snake_case, ProblemDetails com `title` = código de erro,
   `correlation_id` em todos os erros, paginação `page_number`/`page_size`.
6. **Disponibilidade por `is_active`:** `archive`/`reactivate` em vez de soft delete;
   referências históricas continuam legíveis.
7. **Jobs e outbox:** PostgreSQL é a fonte de verdade; QStash só ativa o dispatcher.
8. **Integrações:** Stripe (billing), Cloudinary + Vision (imagens moderadas), Resend
   (email), Google Sign-In, R2 (vídeo, 5D). Todas desligadas por flag até existirem secrets.

## Como regenerar os endpoints

A fonte é o snapshot `docs/api/api-surface.v1.txt` (atualizado pelos testes de contrato
OpenAPI). Depois de alterações à API, regenerar `01_API_ENDPOINTS.md` e confirmar que a
contagem bate com o snapshot. Tipos do frontend são gerados a partir de
`/openapi/v1.json` em Development.
