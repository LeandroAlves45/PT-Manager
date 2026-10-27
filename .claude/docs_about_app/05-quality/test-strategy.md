# Estratégia de testes

Estado: à espera do teu preenchimento (2026-10-14).

Os testes executáveis estão no repositório. Este ficheiro diz o que eles têm de provar. Os comandos estão em `AGENTS.md` e em `.claude/project/03_DEVELOPER_GUIDE.md`.

## O que já existe

| Camada | Onde | O que prova |
|---|---|---|
| Domínio e aplicação | `backend/tests` | Regras e casos de uso no servidor |
| API | `backend/tests` | Contrato, autenticação, tenant |
| Arquitectura | `backend/tests/ArchitectureTests` | Dependências entre projectos |
| Interface | `frontend` (Vitest) | Ecrãs com API simulada |
| Gates manuais | `backlogs/QualityGates.md` | Login real, ainda em aberto em várias fases da 6D, 6E e 6F |

## Lacunas já registadas

- Vários gates `QG6D*`, `QG6E*` e `QG6F*` esperam um login manual teu.
- A 6GA (auth) tem blueprints e ainda não foi aplicada.
- O Sprint 8 é que fecha a suite crítica e o CI de go-live. Até lá, o CI existe e não substitui esse fecho.

## Plano do Sprint 8A (2026-10-27)

Plano: `docs/blueprints/sprint_8/00_plano_sprint_8.md`.

- Cobertura medida com Coverlet (backend) e Vitest v8 (frontend), com gate no CI. Nunca OpenCover.
- E2E com Playwright: registo de trainer, convite e primeiro login do cliente, treino, check-in e pedido de checkout sem Stripe. O billing real em test mode é gate manual no 8C.
- Regras de arquitectura em falta acrescentadas aos `ArchitectureTests`.
- Migrations em produção por workflow GitHub manual, nunca no arranque da API.

## O que queres ver provado e ainda não está nesta lista

**A tua resposta:** por preencher

## Quando uma alteração está pronta, para ti

**A tua resposta:** por preencher
