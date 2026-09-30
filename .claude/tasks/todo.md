# TODO — Sessão 2026-09-30 · Fecho da fatia 6E-3 (biblioteca privada)

Objetivo: fechar a 6E-3 no repositório real — rever 01–06 (aplicados pelo Leandro em `5cd1d7f`),
aplicar o doc 07 (testes), atualizar o doc 08 e `backlogs/QualityGates.md`, CI verde.
Plano aprovado: `~/.claude/plans/6e-3-fecho-moonlit-shamir.md`.

## Decisões (Leandro, 2026-09-30)

- Repor ":" e minúscula em "Item global da plataforma: podes…" e "Arquivado: reativa-o…".
- Repor o texto "Com vídeo" junto ao ícone (acessibilidade).

## Checklist

- [x] Diff programático 01–06 (SHA-256 do doc 01 + diff de blocos)
- [x] Corrigir defeitos: "Com vídeo", `wrap-break-words`, copy "!", JSDoc "admin", `readonly`
- [x] Prettier nos ficheiros tocados + gralhas de comentário
- [x] Aplicar doc 07 (6 criados, 2 substituídos, 2 apagados, excerto em `trainer-fixtures.ts`)
- [x] `npm run test:run` verde; lacunas de cobertura
- [x] Mutações de verificação
- [x] Revisão `code-review-leandro`
- [x] lint, typecheck, test:run, build, Prettier; backend `dotnet test -c Release`; contrato
- [x] Doc 08 + `backlogs/QualityGates.md`
- [x] Relatório `sprint_6E3/09`, GOTCHAS, lessons
- [x] Memória (ACTIVE, MEMORY, NEST, sessão)
- [x] Commit `2ccddb8`, push, CI run #19 verde (4/4)

## Review

6 defeitos corrigidos (D1–D6), 222 testes, M1–M27 27/27 + 6/6, backend 2902; relatório
`sprint_6E3/09`. Registados: pesquisa na página N, metadados antes do AbortController, `video_url`
legado inválido, duplicação do form do admin.
