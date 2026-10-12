# TODO: sessão de 2026-10-11/12 · Sprint 6F-4 (Check-ins e perfil do portal), blueprints

Plano aprovado em `~/.claude/plans/c-users-leandro-alves-desktop-projeto-p-lazy-globe.md`.
Validação na worktree descartável `C:\ptm-tmp-6f4` (branch `tmp/blueprints-6f4`, base `7153956`).
Nada foi aplicado em `frontend/` nem `backend/` do checkout real.

## 1. Preparar
- [x] `git status` limpo e worktree criada
- [x] Watchers verificados e `npm ci` na worktree

## 2. Backend (worktree)
- [x] Provar o 500 do peso sem máximo (provado; as medidas não dão 500: coluna JSON)
- [x] Provar o 500 do email `a@b` (provado em `PATCH /portal/my-profile`)
- [x] Corrigir com testes: `AssessmentLimits.MaxWeightKg`, `EmailAddress.IsValid`, 7 validadores
- [x] Suite completa: 2986 + 1 skip (1 intermitente de moderação, verde ao repetir)
- [x] Contrato sem alterações (só validação)

## 3. Frontend (worktree)
- [x] `api/portal.ts`: chaves e hooks de check-in e perfil
- [x] `lib/checkIn.ts` e `lib/profile.ts`
- [x] `PortalUnavailable`, `StaleDataNotice` e `ClientAvatar`
- [x] Passo a passo do check-in (U20, U21, U22, U26)
- [x] `AvatarSection` e `ProfileContactsForm`
- [x] `PortalCheckInsPage`, `PortalProfilePage`, `index.ts`, `router.tsx`
- [x] `PortalLayout` (V8) e `ProfileMenu` (U23)
- [x] A4 nas 5 páginas existentes
- [x] Retoques design-is (MealCard, SupplementIntakeRow, PortalNutritionPage)

## 4. Testes
- [x] Handlers e fixtures MSW por omissão
- [x] Libs, duas páginas novas, `ProfileMenu`
- [x] Casos novos nos testes dos ecrãs tocados
- [x] Tudo verde na worktree: 544/544 em 53 ficheiros

## 5. Gates na worktree
- [x] lint, typecheck, Vitest completo, build, Prettier, `npm audit`, greps
- [x] Mutações: 57 mortas de 58; M21 era código morto e foi retirado
- [x] Verificação visual (375 px, claro e escuro, ecrã largo)

## 6. Pack
- [x] Gerado por script, `manifest.json`, round-trip 53/53
- [x] Docs 01–08 em `sprint_6F/sprint_6F4/`

## 7. Documentação
- [x] `00_plano_fase_6F_completa.md` (reposto depois do crash e atualizado) e `10_fecho_sprint_6F.md`
- [x] `backlogs/QualityGates.md`
- [x] `05_ECRAS_CLIENTE.md`, sprint-6 README, `plan_sprint_6_por_atualizar.md`
- [x] Memória (ACTIVE, MEMORY, NEST, nota de sessão, lessons, GOTCHAS)

## 8. Descartar
- [x] Worktree e branch apagados; `git status` final sem `frontend/` nem `backend/`

## Review

- Pack `sprint_6F4/01–08` + manifest: 53 caminhos (44 integrais, 9 patches), round-trip 53/53.
- Decisões U20–U26 do Leandro. O check-in ficou em três passos num só formulário.
- A confirmação do contrato por teste funcional encontrou dois 500 no backend (peso corporal
  sem teto e email `a@b`); corrigidos no doc 02, sem contrato novo.
- Achados fora do pack, no backlog do doc 08: `rate_limit_exceeded` nunca é emitido pelo
  backend; medidas sem teto; três casas decimais partem a idempotência; `.EmailAddress()` nos
  validadores de autenticação.
- Não foi feito: mutações de backend e teste funcional do formato `Feedback[n]` (o Docker
  deixou de responder depois do primeiro reinício da máquina); envio de um ficheiro real no
  browser.
- Incidente: um reinício deixou `00_plano_fase_6F_completa.md` a zeros (`docs/` não está no
  git). Reposto a partir do conteúdo lido no início da sessão; convém o Leandro confirmar que
  o ficheiro está como esperava.
- Ficam com o Leandro: aplicar o pack, testes antes do push e os gates manuais
  (`QG6F4-UI-001`, `QG6F4-ISOLAMENTO-UI-001`, `QG6F4-AVATAR-001`, `QG6F-GATE-001`).

**Blueprints da fase 6F-4: Finalizado.** A fase fecha depois da aplicação e dos gates.
