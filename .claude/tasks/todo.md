# TODO — Sessão 2026-09-23 · Sprint 6D, blueprints admin

Objetivo: gerar blueprints de código real a partir de implementação validada apenas na worktree `sprint-6d-planning`. O projeto principal recebe somente documentação.

## Decisões

- React 19, TypeScript 5.9.3, Vite 8, Tailwind 4 e shadcn/ui atuais. Sem dependências novas.
- Cinco rotas admin: visão geral, moderação e três catálogos globais.
- Estrutura comum de listagem e formulário próprio por catálogo.
- Vídeo global: mostrar estado e disponibilidade do vídeo anterior; criação e remoção pelo admin ficam para fase futura.
- Contrato aditivo `managed_video_status` e `has_ready_video`, classificado como Preserve.
- Testes frontend em `frontend/src/test/`, a espelhar `src/`.

## Trabalho

- [x] Ler Sprint Pack, documentação canónica, 6C e skills relevantes.
- [x] Confirmar estado limpo de `main` antes do trabalho.
- [x] Criar worktree temporária `sprint-6d-planning`.
- [x] Materializar contrato backend e cinco ecrãs na worktree.
- [x] Gerar tipos OpenAPI e confirmar igualdade com API temporária.
- [x] Fechar testes, build, typecheck, lint global inicial e ESLint dirigido final; verificar contrato, orçamento SQL, 403 e auditoria.
- [x] Inspecionar catálogo de alimentos a 375/768/1440 px, claro/escuro, e formulário por teclado; visão geral e moderação a 375 px. Restantes ecrãs com login real ficam no gate manual.
- [x] Extrair blueprints completos, excertos de ficheiros extensos e quality gates.
- [x] Conferir 19 blocos e dois excertos com os 21 ficheiros testados.
- [x] Atualizar NEST, ACTIVE, Sprint Pack e nota de sessão com obsidian-ptmanager.
- [x] Remover worktree e branch, sem merge; confirmar que frontend/backend principais não mudaram.

## Regra de fecho

A worktree valida os blueprints. O gate de implementação real da 6D só fecha após Leandro aplicar os ficheiros e repetir a validação no projeto principal.
