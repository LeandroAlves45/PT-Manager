---
name: blueprints-codigo-real
description: >
  Formato e regras para blueprints de código real (backend C# ou frontend TS/TSX),
  documentos .md com código completo, pronto a copiar, sem implementação directa no
  projecto. Usar quando o pedido for "gera os blueprints de código real", "converte o
  pseudocódigo em código real", "documenta este ficheiro em código real", ou quando o
  utilizador referenciar o pack de blueprints de uma fase. Complementa a skill
  sprint-context, que trata de qual é a fase activa; esta skill trata de como escrever
  cada documento depois de saber o quê escrever.
---

# Blueprints de código real

Substitui `.claude/memory/Patterns/blueprints_codigo_real_por_ficheiro.md` e
`blueprints_codigo_real_frontend.md` (retirados em 2026-09-21, fundidos aqui). O núcleo
abaixo é comum a backend e frontend. Regras e checklist específicos de cada stack estão em
`references/backend-csharp.md` e `references/frontend-typescript.md` — ler o que
corresponder à linguagem do ficheiro em documentação, não os dois de cada vez.

## Quando usar

O utilizador pede blueprints com código real, sem implementação directa no projecto. A
origem pode ser um pseudocódigo já aprovado ou um desenho funcional. Não é obrigatório
produzir pseudocódigo intermédio.

## Objectivo

Permitir que o programador implemente manualmente cada ficheiro a partir de código
completo, coerente com o projecto e pronto para copiar, sem confundir documentação com
estado implementado.

## Estrutura obrigatória por ficheiro documentado

1. Caminho exacto a partir da raiz do repositório.
2. Estado real: **a criar**, **existente, substituir integralmente** ou **existente,
   ajustar**. Confirmado com `Glob`/`Read` (ou `ls`/`find`) antes de escrever — nunca
   assumido a partir da memória de uma sessão anterior.
3. Camada e responsabilidade (ver a referência da stack para o que isso significa em cada
   caso).
4. Um único bloco contínuo, na linguagem do ficheiro, com o conteúdo **completo**.
   Excepção: ficheiros existentes com mais de 200 linhas usam o modo excerto abaixo.
5. Notas de mentor: decisões que o código não torna evidentes por si.
6. Validações e testes que provam o ficheiro, a seguir ao bloco.
7. Quality gate aplicado, com critério objectivo.

Regras de conteúdo do próprio bloco (imports/usings, tipagem, comentários, DI,
concorrência) estão na referência da stack, porque diferem o suficiente para não valer a
pena forçar uma redacção comum.

## Modo excerto para ficheiros extensos

O formato depende do tamanho do ficheiro real **no commit base**:

| Situação | Formato |
|---|---|
| Ficheiro novo | Bloco único e completo |
| Existente com ≤ 200 linhas | Bloco único e completo ("existente, substituir integralmente") |
| Existente com > 200 linhas | **Excerto** ("existente, ajustar") |

Cada excerto contém:

1. **Âncora exacta:** o membro/método onde a mudança entra, citando a linha anterior e a
   seguinte que ficam inalteradas (texto literal, para localizar sem ambiguidade).
2. **O membro ou bloco completo** que é acrescentado ou substituído — nunca `...`, "resto
   igual" ou corpos omitidos *dentro* do bloco.
3. Marcadores de mudança em comentário (ex. `// [6C] NOVO:`, `// [6C] ALTERADO:`,
   `// [6C] REMOVIDO:`, prefixo da fase em curso), a explicar o quê e porquê.
4. Imports/usings novos listados à parte, antes dos excertos.
5. Acção explícita por excerto: "inserir depois de", "substituir o método", "remover".

O modo excerto **só** se aplica a ficheiros que já existem. Nunca apresentar como "a criar"
um esqueleto de um ficheiro que já existe; nunca apresentar um ficheiro existente extenso
como "substituir integralmente" com conteúdo resumido.

Validação obrigatória dos excertos: aplicar cada excerto ao ficheiro original do commit
base e comparar o resultado, byte a byte (normalizando só fins de linha), com o ficheiro
materializado e testado; qualquer diferença falha a entrega.

## Preservação do desenho aprovado

Gerar código real altera a representação, não os requisitos. Mantêm-se sempre:

1. As mesmas responsabilidades por ficheiro.
2. Os mesmos casos de uso, contratos e invariantes.
3. As mesmas categorias e códigos de erro estáveis.
4. As mesmas garantias de multi-tenancy, atomicidade e concorrência.
5. Os mesmos cenários de teste e critérios de aceitação.

Quando a origem funcional ou o pseudocódigo contradiz o código atual ou os documentos
canónicos, prevalece a fonte de verdade com maior precedência (ver AGENTS.md, secção
Prioridade de decisão) e a correcção é registada na nota de mentor do blueprint afectado.

O bloco não pode remeter para passos abstractos, lógica descritiva, código a completar
posteriormente ou uma implementação existente sem a reproduzir.

## Estado de implementação

Código apresentado num ficheiro Markdown não está implementado. O estado só muda depois de
o respectivo ficheiro real ser criado ou alterado, compilado e validado no projecto. Não se
modifica `backend/src`, `backend/tests`, migrations nem `frontend/` durante uma sessão que
autorize apenas conversão documental.

A validação materializa-se numa worktree temporária, criada no início da sessão e apagada
no fim — método permanente descrito em `.claude/memory/NEST.md`, secção 7. Nunca worktree
permanente, nunca merge.

## Validação obrigatória comum às duas stacks

1. Cada caminho documentado tem exactamente um bloco completo.
2. Zero placeholders, `TODO`, corpos omitidos ou pseudocódigo dentro do bloco.
3. Diff programático e normalizado entre cada bloco e o ficheiro materializado.
4. Duplicação intra-lote verificada: lógica repetida entre ficheiros do mesmo lote
   extrai-se para um helper partilhado sempre que isso não violar YAGNI; quando a
   duplicação é deliberada, registar o motivo na nota de mentor em vez de a extrair.
5. Quando um ficheiro do lote depende de um tipo ou decisão definido noutro ficheiro do
   mesmo lote, declarar essa dependência e a ordem de aplicação obrigatória na nota de
   mentor — nunca assumi-la implícita.
6. Antes de marcar um ficheiro "a criar", confirmar com `Glob`/`Read` que o caminho não
   existe já no projecto real. Um ficheiro já existente só pode ser "existente, substituir
   integralmente" ou "existente, ajustar" — nunca "a criar" com um esqueleto reduzido:
   copiar esse esqueleto por cima do ficheiro real apagaria produção.
7. Manter rastreabilidade entre requisito, ficheiro de produção e teste que o prova.
8. `git status` no repositório real prova que nenhum ficheiro real mudou.
9. Criar o documento de quality gates da fase e actualizar `backlogs/QualityGates.md`.

Checklist adicional, específico da stack: ver `references/backend-csharp.md` ou
`references/frontend-typescript.md`.

## Critério de qualidade

Um programador deve conseguir copiar o bloco para o caminho indicado, compreender as
decisões relevantes e iniciar a validação do ficheiro sem completar lógica em falta ou
decidir novamente a arquitectura.
