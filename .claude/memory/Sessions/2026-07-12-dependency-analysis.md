# Sessão — Análise de Dependências (12-07-2026)

## Pontos fundamentais

- Foram lidos `backend/requirements.txt`, `backend/pyproject.toml`,
  `frontend/package.json` e `frontend/package-lock.json`.
- Foram analisadas 85 dependências diretas únicas e pesquisadas as versões
  estáveis prioritárias em PyPI/npm e changelogs oficiais.
- `npm outdated` confirmou versões atuais e `npm audit` encontrou 18
  vulnerabilidades: 10 altas, 6 moderadas e 2 baixas.
- As correções mais urgentes são Axios 1.18.1, React Router DOM 7.18.1,
  PostCSS 8.5.17 e Vite 7.3.6.
- React deve permanecer na major 19; Vite 8 e ESLint 10 serão migrações
  separadas com Node.js 22.13+.
- O backend não tem lockfile e as declarações de `requirements.txt` e
  `pyproject.toml` divergem.
- A autenticação Snyk falhou; falta uma auditoria automatizada Python.
- Não foi executada qualquer instalação ou atualização de dependências.

## Artefactos

- `.claude/dependency-update-plan.md`
- `.claude/memory/gotcha_dependency_updates.md`
- `tasks/todo.md`

Finalizado
