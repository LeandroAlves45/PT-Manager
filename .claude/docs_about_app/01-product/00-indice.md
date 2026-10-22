# 01 — Produto

Finalidade: dizer que problema o PT Manager resolve, para quem, e o que entra no âmbito.

Estado da pasta: visão em rascunho. A análise tem três dores. A faturação e o pack pendente entram depois do go-live.

## Ficheiros

| Ficheiro | Função | Estado |
|---|---|---|
| `product-vision.md` | Resumo, problema, público, âmbito do MVP até ao go-live, fora de âmbito, limitações | Rascunho de 2026-10-14. Âmbito, problema e pagamento confirmados. |
| `problem-analysis.md` | Três dores: trabalho espalhado, esquecimento, faturação opaca | Relato organizado. A regra da validação está escrita. Falta a origem do valor. |

## O que não se cria aqui

O roadmap de sprints já vive em `.claude/project/02_SPRINTS_ROADMAP.md`. Esta pasta não leva um `roadmap.md` paralelo. Se a visão precisar de horizonte, referencia esse ficheiro e distingue capacidade existente de trabalho futuro.

## Próximo passo

Os prints do portal entraram a 2026-10-22 e a lista do cliente está aplicada (`sprint_6GB/04_plano_6GB_cliente.md` e `05_relatorio_fecho_6GB_cliente.md`). O exemplo «ex.: Barra» aparece no admin e no treinador. O botão «Guardar fuso» só parece pronto quando o fuso mudou, e o erro do logótipo fica ao lado do logótipo. Os filtros da moderação continuam na mesma linha e o seleccionado tem de se ver bem. O fuso horário passa a uma lista com pesquisa, na 6GB-1. A avaliação inicial do treinador abre numa página própria, na 6GB-2. Suplemento de cliente activo aparece na lista; se o cliente estiver arquivado, sai da lista activa e os dados ficam. Os treinadores têm os mesmos ecrãs de marca; só muda a marca que cada um guardou. «Criar alimento» e «Criar suplemento» seguem o exercício: título no singular e botão «Criar»; «Guardar» fica para editar. Um campo numérico de gramas não aceita «5gr». A cor da marca escreve-se em hex e num seletor, lado a lado (6GB-1). O vídeo do exercício entra já na criação, na 6GB-1. A visão geral do admin fica sem gráficos na 6GB. O destaque azul fica só no cartão que precisa de atenção. A marca muda em todas as páginas da área do treinador, tal como no portal (6GB-2). Há um botão «Copiar link» e o link não fica sempre à vista. Momento do suplemento não é obrigatório e pode ficar vazio. «gr» conta como «g» e guarda-se «g». Repetições fora do intervalo são aceites e a app não avisa (6GB-2, 2026-10-15). O intervalo escreve-se com o maior primeiro, como 12-10. O máximo é opcional: um só número é o alvo (6GB-2, 2026-10-15). O treinador continua a corrigir as séries e não as apaga. Email da ficha diferente do convite: a ficha mostra o convite do email antigo, com a data. Convite inválido do cliente: «O convite não é válido.» Convite já usado: «Este convite já foi usado.» Isento e bloqueado entra nos dois números, isentos e conta bloqueada, e não entra na subscrição activa (2026-10-15, `RN-018`). Bloqueado com subscrição activa entra nas duas contagens. Trial: conta na subscrição activa e o zero não entra no dinheiro. O reembolso ficou na `RNF-014`, sem prioridade: só no Stripe, sem ecrã; o painel desce quando o Stripe confirmar, só a parte devolvida.
