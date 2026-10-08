# TODO: sessão de 2026-10-04 · Sprint 6F (planeamento e blueprints da 6F-1)

Plano aprovado em `~/.claude/plans/twinkly-floating-conway.md`. As decisões U1–U10 estão no
`00` da 6F.

## Checklist

- [x] Pesquisa de documentação (.claude, docs, a 6E como modelo, NEST, ACTIVE)
- [x] Perguntas de clarificação (U1–U10)
- [x] Pesquisa de segurança das dependências: zero pacotes novos, OSV/GHSA sem advisories,
  `npm audit` 0
- [x] `sprint_6F/00_plano_fase_6F_completa.md` e esqueleto `10_fecho_sprint_6F.md`
- [x] Worktree `C:\ptm-tmp-6f` (base `134139c`) e linha de base
- [x] Backend 6F-1: seed (check-in do João, marca do trainer 1, `cliente2@` da Marta) e
  testes 10/10
- [x] Frontend 6F-1: contraste, marca, layout, barra com 5 itens, Início, pré-visualização da
  6E-5, testes 315/315
- [x] Gates na worktree: backend 2920, lint, typecheck, build, Prettier, greps
- [x] Mutações FM1–FM19 e MU1–MU5: 24/24 mortas
- [x] Pack `sprint_6F1/01–08` + manifest extraído; round-trip SHA-256 31/31, com os gates
  repetidos
- [x] Worktree e branch descartados; `frontend/` e `backend/` intactos
- [x] Atualizados: `plan_sprint_6`, README do sprint 6, layout 05, QualityGates, lessons
- [x] Memória: sessão, MEMORY, NEST e ACTIVE, mais a memória automática

## Review

- **Entregue:**
  - o plano do sprint 6F inteiro (4 fases, contratos `/portal/*` com `ficheiro:linha`,
    armadilhas);
  - o pack validado da 6F-1 (31 caminhos);
  - o plano do Sprint 6 atualizado.
- **Desvio do plano:** as verificações novas do seed ficaram **obrigatórias**, e não
  condicionais, seguindo o precedente do 2.º trainer. Por isso, uma BD dev antiga exige
  reset (`QG6F1-RESET-001`). A justificação está no doc 01 e na nota de mentor do doc 02.
- **Pendente para o Leandro:**
  1. aplicar os docs 02–07 (o 07 no mesmo commit);
  2. fazer o reset da BD dev;
  3. correr os gates manuais `QG6F1-UI-001` e `QG6F1-ISOLAMENTO-UI-001`;
  4. confirmar o CI.
- **Próxima sessão:** blueprints da 6F-2.
