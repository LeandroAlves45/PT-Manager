# Documentação de produto — PT Manager

Pasta de planeamento do produto. Descreve o que o PT Manager é, para quem, que regras segue e que requisitos tem de cumprir. Não substitui a arquitectura, o esquema, o roadmap nem os blueprints.

O desenho desta pasta está em `documentacao_pt_manager.md` (secções 4 e 5). A localização foi adaptada: o documento sugere `docs/`, mas essa pasta está no `.gitignore` e já guarda blueprints. O sítio estável e versionável é `.claude/docs_about_app/`.

## O que não se duplica

| Tema | Fonte de verdade |
|---|---|
| Arquitectura | `.claude/project/00_ARCHITECTURE.md` |
| Esquema da base de dados | `.claude/project/01_DATABASE_SCHEMA.md` |
| Roadmap e âmbito dos sprints | `.claude/project/02_SPRINTS_ROADMAP.md` |
| Como correr o projeto | `.claude/project/03_DEVELOPER_GUIDE.md` e `AGENTS.md` |
| Contrato HTTP | `.claude/project/backend/` |
| Ecrãs e layout | `.claude/project/frontend/` |
| Blueprints de implementação | `docs/blueprints/` |
| Gates e itens diferidos | `backlogs/` |

`04-architecture/` e `06-project-management/` só apontam para essas fontes e registam o que elas não cobrem (decisões ainda soltas, riscos transversais). Não se cria um segundo roadmap nem uma segunda arquitectura.

## Estados

Cada requisito, regra ou funcionalidade usa um destes estados:

- `Implementado` — confirmado no código e, quando aplicável, em testes.
- `Parcialmente implementado` — existe parte do percurso; falta completar ou validar.
- `Planeado` — aprovado, ainda não implementado.
- `Por validar` — hipótese ou decisão à espera de confirmação.
- `Descontinuado` — saiu do âmbito; fica só como histórico.

Um ecrã, um modelo ou um endpoint, sozinhos, não chegam para marcar `Implementado`.

## Árvore

```text
.claude/docs_about_app/
├── documentacao_pt_manager.md
├── README.md
├── 01-product/
├── 02-requirements/
├── 03-functional-design/
├── 04-architecture/
│   └── decisions/
├── 05-quality/
└── 06-project-management/
```

Cada pasta tem `00-indice.md`. Os ficheiros de conteúdo estão criados. O que falta da tua parte está marcado **por preencher**.

## Ordem de preenchimento

1. Visão e âmbito (`01-product`).
2. Stakeholders, requisitos e regras (`02-requirements`).
3. Permissões e fluxos críticos (`03-functional-design`).
4. Apontadores de arquitectura, qualidade e riscos (`04` a `06`), sem copiar o que já está canónico.

Identificadores estáveis: `RF-`, `RNF-`, `RN-`, `UC-`. Cada documento referencia os outros em vez de repetir o texto.

## Como se preenche

Os ficheiros de conteúdo já existem. O que está escrito veio das tuas respostas de 2026-10-14 ou dos documentos canónicos, com a fonte indicada. O que falta é teu e está marcado **por preencher**.

Substitui cada **por preencher** pela tua resposta. Não apagues o identificador (`RF-`, `RNF-`, `RN-`, `UC-`). Quando terminares um ficheiro, pede a revisão: eu confronto as respostas com o código e devolvo perguntas só sobre o que continuar incompleto ou em conflito.

Uma afirmação sobre o comportamento actual só passa a `Implementado` com evidência no código ou nos testes.
