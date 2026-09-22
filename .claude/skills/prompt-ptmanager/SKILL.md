---
name: prompt-ptmanager
description: Gera o prompt completo e pronto a colar no Claude Code para o PT Manager, no formato que Leandro ja usa (fecho de fase: review, testes e quality gates; abertura de fase: planeamento e blueprints). Usar quando ele pede para preparar o proximo prompt para o Claude Code, fechar uma fase de sprint, ou planear a fase/sprint seguinte.
---

# Objetivo

Esta skill nao executa o trabalho de sprint. Gera o prompt estruturado que Leandro cola no Claude Code, seguindo exatamente o formato e as regras fixas que ele ja usa no projeto PT Manager. A skill nunca inventa a parte variavel: se faltar informacao essencial, pergunta antes de gerar o prompt final. A entrega e sempre o texto do prompt pronto a copiar, nao uma explicacao sobre como o escrever.

# Sintaxe fixa

`/goal` e `/caveman` sao sempre escritos com barra e sempre no inicio da linha de comando, nesta ordem, porque Leandro quer que fiquem injetados desde o primeiro instante da sessao, o goal para fixar o objetivo antes de qualquer outra coisa, o caveman para cortar falas desnecessarias desde o inicio.

Todas as restantes skills mencionadas no prompt (testing, code-review-leandro, blueprints-codigo-real, obsidian-ptmanager, ponytail-ptmanager, sprint-context, performance-reviewer, security-audit, security-reviewer, etc.) sao escritas sem barra, soltas no texto, nunca no formato `/nome`. Isto e deliberado: evita que o Claude Code as injete automaticamente quando nao e essa a intencao.

# Deteção do modo

Modo fecho de fase: Leandro diz que terminou uma implementacao, quer rever o que fez, correr testes, ou fechar uma fase do sprint.

Modo abertura de fase: Leandro quer planear a fase ou sprint seguinte, gerar blueprints, ou iniciar uma nova fase.

Se nao for claro qual dos dois modos se aplica, perguntar antes de continuar.

# Modo fecho de fase

## Variaveis a recolher (perguntar as que faltarem)

- Numero da fase/sprint (ex: 6C)
- Diretorio de blueprints da fase (ex: docs\blueprints\frontend-files\sprint_6\sprint_6C)
- Ate que ficheiro/funcionalidade foi implementado (resumo curto)
- Camada em causa: backend ou frontend, para definir o papel e a regra de nao tocar na outra camada
- Skill de revisao de codigo a usar: por omissao code-review-leandro; perguntar se deve trocar ou somar performance-reviewer ou security-audit/security-reviewer quando a fase envolver areas sensiveis a performance ou seguranca.
- Skills adicionais que ele queira invocar nesta sessao, para alem das fixas

Nao perguntar nem mencionar seeds de base de dados. Isto nao faz parte do fluxo habitual; so entra no prompt se Leandro o disser explicitamente naquela sessao, sem a skill sugerir ou antecipar.

## Estrutura de saida fixa

Linha 1: `/goal <objetivo curto de uma frase: rever, testar cobertura de caminhos, atualizar documentacao> /caveman` seguido dos nomes das skills de trabalho sem barra, sempre incluindo testing e a skill de revisao escolhida, mais as que ele pedir.

Bloco de papel: "Claude atua como um senior developer e expert em <backend/frontend>, com base no diretorio '<diretorio>' onde estou a implementar por ordem [detalhar a ordem/dependencias que ele mencionar]."

Lista numerada de funcoes, incluir sempre por omissao, salvo indicacao contraria de Leandro:

1. Analisar o que foi implementado e verificar se ficou tudo correto.
2. Nao tocar na camada ainda nao implementada (frontend ou backend, conforme o caso).
3. Realizar apenas testes que cobrem caminhos reais, sem testes desnecessarios, e acrescentar mais se detetar lacunas de cobertura.
4. Usar a skill de revisao de codigo escolhida para validar a implementacao.
5. Se usar agentes, usar o modelo sonnet 5.
6. Atualizar os quality gates cumpridos, tanto no diretorio da fase como em backlogs\QualityGates.md. (Confirmar com Leandro se ainda aplicavel; se ele disser que ja nao segue este ficheiro, remover este ponto.)
7. No fim, criar ficheiro(s) .md com o que foi feito, achados e correcoes realizadas, e a revisao da fase, marcando-a como finalizada.
8. Atualizar a documentacao com a skill obsidian-ptmanager.
9. Garantir que tudo fica verde antes de terminar.

Nota final fixa: mencionar que o docker esta a correr.

# Modo abertura de fase

## Variaveis a recolher (perguntar as que faltarem)

- Fase anterior concluida e fase seguinte a planear
- Diretorio de blueprints alvo
- Camada em causa: backend ou frontend
- Se e necessaria pesquisa de dependencias estaveis, e para que camada
- Nome da worktree a criar
- Anexos disponiveis (planos previamente escritos) a referenciar com @caminho

A skill blueprints-codigo-real e sempre incluida na lista de skills deste modo, sem perguntar. O antigo ficheiro `.claude\memory\Patterns\blueprints_codigo_real_por_ficheiro.md` foi apagado e substituido por esta skill; nunca referenciar esse caminho.

## Estrutura de saida fixa

Linha 1 (se houver anexo): `@"<caminho completo do anexo>"`

Linha seguinte: `/goal <objetivo da fase> /caveman` seguido dos nomes das skills de trabalho sem barra: blueprints-codigo-real sempre presente, mais testing, ponytail-ptmanager e obsidian-ptmanager quando aplicavel dentro da worktree.

Bloco de papel: "Claude atua como um senior developer e expert em <frontend/backend>. Vais atuar com confirmacoes, validacoes e pesquisas, sem suposicoes ou achismos."

Bloco de contexto: fase anterior concluida, fase seguinte, diretorio de blueprints alvo, e que os blueprints seguem o formato definido na skill blueprints-codigo-real.

Lista numerada de funcoes, incluir sempre por omissao, salvo indicacao contraria de Leandro:

1. Analisar toda a documentacao necessaria dentro de .claude e docs; se necessario, criar um indice para sessoes futuras saberem onde encontrar a documentacao.
2. Se for preciso atualizar dependencias, pesquisar na internet quais estao estaveis para a camada em causa, tendo em conta o design em .claude\project\frontend quando for frontend; nunca introduzir dependencias inseguras.
3. Usar o anexo (plano previamente feito) como ponto de partida e contexto adicional, se existir.
4. Fazer perguntas de clarificacao em linguagem muito simples (como para uma crianca de 5 anos), explicando trade-offs quando existirem, antes de avancar.
5. Apagar o todo.md anterior e criar um novo para esta sessao.
6. Se usar agentes, usar o modelo sonnet 5.
7. Criar a worktree indicada como sandbox de validacao: implementar la o codigo necessario apenas para confirmar que o plano funciona na pratica, extrair os blueprints completos e corretos (seguindo a skill blueprints-codigo-real) a partir dessa implementacao testada, e no final descartar a worktree por inteiro. Nunca fazer merge da worktree para o branch principal, e nunca deixar codigo dessa implementacao a viver no projeto principal; a implementacao real fica sempre a cargo de Leandro, feita a partir dos blueprints gerados.
8. Dentro da worktree, usar as skills testing e ponytail-ptmanager conforme necessario para a validacao.
9. No fim, usar obsidian-ptmanager para atualizar a documentacao com o que foi decidido e os blueprints gerados.

Nota final fixa: mencionar que o docker esta a correr, e que ajustes pontuais no backend podem ficar no mesmo diretorio da fase em vez de separados.

# Regra geral

Entregar sempre o prompt completo em bloco de texto pronto a copiar para o Claude Code, escrito em portugues europeu, sem inventar variaveis que Leandro nao deu. Se alguma variavel essencial faltar, perguntar antes de fechar o prompt.
