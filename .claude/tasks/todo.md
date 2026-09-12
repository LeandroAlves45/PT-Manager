# Sprint 5 — Fase 5C: imagens geridas (logo + avatar)

Plano aprovado: `C:\Users\Leandro Alves\.claude\plans\c-users-leandro-alves-desktop-projeto-p-glittery-adleman.md`
Materialização: `C:\ptm5c` (cópia de `backend/`, fora do repositório — `backend/src`,
`backend/tests` e as migrations reais permanecem intocados).

## 0. Preparação

- [x] Cópia do backend para `C:\ptm5c` sem `bin`/`obj` (1210 ficheiros .cs)
- [x] `dotnet restore --locked-mode` verde na cópia
- [x] Build Release de baseline verde na cópia (0 warnings, 0 erros)
- [x] Baseline registada: 2171 testes antes da fase (ACTIVE.md, fecho 5B)

## 1. Materialização do código real (por lote, na ordem obrigatória)

- [x] A/01 — Portas e contratos de media na Application (~12 ficheiros)
- [x] B/02 — Domain: `Client.AvatarPublicId`, `ReplaceAvatar`, `RemoveAvatar`, −`SetAvatar`
- [x] C/03 — Application: logo (`ReplaceLogoHandler`, validator, erros)
- [x] D/04 — Application: avatar (store port, commands, validators, 2 handlers, DI)
- [x] E/05 — Infrastructure: `SkiaImageProcessor`, `BoundedStreamReader`
- [x] F/06 — Infrastructure: Cloudinary (options, validator, assinatura, transport, storage)
- [x] G/07 — Infrastructure: moderação Vision (options, validator, token provider, service)
- [x] H/08 — Infrastructure: `MyAvatarStore`, `ClientLocking`, configurations, 2 handlers de outbox
- [x] I/09 — Composition root, packages, configuração, logging
- [x] J/10 — Api: binder multipart, rotas, rate limit
- [x] K/11 — Migration `AddManagedImageAssets` + os dois preflights SQL
- [x] L/12 — Testes (todas as suites) + reescrita dos 3 testes bloqueadores

## 2. Validação executável na cópia

- [x] `dotnet restore` regenerou 5 lock files; locked-mode de baseline verde
- [x] `dotnet build -c Release` e `-c Debug`: 0 warnings, 0 erros
- [ ] 5 suites verdes — PARCIAL: Domain 432, Application 569, Architecture 65, Integration sem PostgreSQL 79, contrato OpenAPI 3; PostgreSQL e Functional NÃO executados (Docker)
- [x] `dotnet format --verify-no-changes` exit 0
- [x] `has-pending-model-changes` limpo (binários de Debug)
- [ ] Ciclo da migration em PostgreSQL 17 — NÃO executado (Docker); SQL Up/Down gerado e revisto
- [x] Snapshot OpenAPI regenerado: exatamente +3 operações

## 3. Blueprints (extraídos da cópia, nunca escritos à mão)

- [x] `00_desenho_aprovado_indice_dependencias_gates.md`
- [x] `01` a `12` — 91 blocos integrais
- [x] `13_qa_da_fase.md`
- [x] `14_rastreabilidade_revisao_quality_gates.md` com validação pendente declarada
- [x] Comparação programática: 91/91 exatas

## 4. Fecho

- [x] `backlogs/QualityGates.md` — secção 5C com 4 gates abertos
- [x] `.claude/memory/ACTIVE.md` e `MEMORY.md` atualizados
- [x] Nota de sessão `Sessions/2026-09-11-sprint5c-blueprints.md`
- [x] `C:\ptm5c` eliminado
- [x] Confirmar por `git status` que nada em `backend/` foi tocado, nada pelo claude durante
o processo

## Review

Pack completo e verificado onde a execução foi possível. Dois defeitos reais encontrados
pela materialização (PNG truncado aceite pelo SkiaSharp; eliminação sem prova de posse
pelo tenant) e corrigidos antes da extração. A fase NÃO está fechada: faltam a parte
PostgreSQL das suites Infrastructure e Functional e o ciclo da migration, por o Docker
não ter arrancado. Gates `QG5C-TEST-003` e `QG5C-MIG-001` abertos.
