# PT Manager — Guia de Arquitetura em Camadas

Este projeto usa separação em camadas **pragmática**. O objectivo é manter responsabilidades claras sem over-engineering.

## Regras por Camada

### `db/models/` — Entidades

- SQLModel: combina ORM + validação Pydantic
- Sem lógica de negócio complexa; apenas campos, relações e constraints
- `trainer_id` em entidades tenant-scoped

### `repositories/` — Acesso a Dados

- Queries SQLModel/SQLAlchemy
- Métodos nomeados por intenção: `get_by_id_for_trainer`, `list_active_for_trainer`
- **Sempre** receber `trainer_id` quando aplicável
- Se não existir repo para um domínio, criar antes de aceder DB no service

### `services/` — Lógica de Negócio

- Orquestra repos, validações, side-effects (email, Stripe)
- Não conhece HTTP (sem `Request`, `HTTPException` aqui — devolver resultado ou levantar excepções de domínio)
- Excepção pragmática: alguns services actuais levantam `HTTPException` — preferir migrar para routes

### `api/routes/` — HTTP

- Thin controllers: validar input (schemas), chamar service, mapear resposta
- `Depends(get_current_user)`, `Depends(require_api_key)` no router ou endpoint
- Guards de role e subscrição aqui ou em deps dedicados

### `api/schemas/` — Contratos API

- Pydantic models para request/response
- Separados dos models ORM (não expor campos internos)

## O que NÃO fazer

| Anti-pattern                    | Porquê                                   |
| ------------------------------- | ---------------------------------------- |
| Query SQL directa em routes     | Bypassa tenant isolation e testabilidade |
| Lógica de negócio em routes     | Dificulta testes e reutilização          |
| Editar migration SQL existente  | Pode já ter corrido em produção          |
| Confiar em `trainer_id` do body | Extrair sempre do JWT autenticado        |
| Hardcode de secrets             | Usar `Settings` via env vars             |

## Nova Feature — Sequência Recomendada

1. **Model** — `db/models/nova_entidade.py` + export em `__init__.py`
2. **Migration** — novo `NNN_descricao.sql` em `db/migrations/`
3. **Repository** — `repositories/nova_entidade_repo.py`
4. **Service** — `services/nova_entidade_service.py`
5. **Schemas** — `api/schemas/nova_entidade.py`
6. **Route** — `api/routes/nova_entidade.py` + registo em `main.py`
7. **Test** — unit no service, integration na route

## Frontend — Padrão Recomendado

1. Módulo API em `frontend/src/api/novaEntidade.js`
2. Hook opcional em `hooks/useNovaEntidade.js`
3. Page em `pages/trainer/NovaEntidadePage.jsx`
4. Componentes extraídos quando page > ~300 linhas

## Multi-Tenant — Checklist

- [ ] Query filtra por `trainer_id` do utilizador autenticado
- [ ] Endpoint valida que recurso pertence ao trainer antes de update/delete
- [ ] Client role só acede aos seus próprios dados
- [ ] Superuser bypass documentado e intencional
