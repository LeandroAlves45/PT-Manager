# Análise do problema

Estado: relato de 2026-10-14 organizado em três dores. A faturação e o pack pendente ficam para depois do go-live.

Fonte: o responsável do produto descreveu o processo que quer substituir. Não há entrevistas a outros treinadores no repositório. Isso é evidência de uma pessoa, não de um mercado.

## 1. O que a app tem de fazer

Relato organizado, sem acrescentar funções:

- Ter os clientes arrumados.
- Construir planos de treino e de alimentação. A suplementação entra na refeição quando o treinador a quiser aí.
- Se o cliente tiver aulas presenciais, ter packs de sessões no modelo de trabalho desse treinador e atribuí-los ao cliente.
- Registar a avaliação inicial com os campos que importam, e avaliações periódicas no modelo desse treinador.
- Enviar email ao cliente no dia da sessão, podendo ser na véspera, e também ao treinador.
- Dar ao cliente o planeamento num só sítio, com aviso de sessão e de check-in.

## 2. O que acontece hoje, sem a app

- O plano de treino vai em Excel.
- A alimentação vai em Word.
- Os clientes organizam-se à mão, ou ficam desorganizados.
- O treinador não sabe ao certo quanto fatura, ou aponta isso à mão.
- O check-in tem de ser lembrado à mão, e o cliente esquece-se do dia.

## 3. Três dores

| Campo | Problema 1 — trabalho espalhado | Problema 2 — esquecimento | Problema 3 — faturação opaca |
|---|---|---|---|
| Problema | Planos e clientes vivem em ficheiros e em listas à mão. | Sessões e check-ins passam porque ninguém é avisado a tempo. | O treinador não sabe quanto fatura, ou só o sabe num apontamento manual. |
| Quem é afectado | O treinador, e o cliente que recebe ficheiros soltos. | O cliente, que esquece. O treinador, que também quer o aviso. | O treinador. |
| Como é feito hoje | Excel para o treino. Word para a alimentação. Clientes à mão. | Aviso manual do check-in. Sem aviso fiável da sessão. | Contas à mão. |
| O que corre mal se continuar assim | Não há uma versão única do plano nem um sítio único para o cliente. | Faltam check-ins e o dia da sessão surpreende. | O treinador não consegue dizer o que facturou. |
| Alternativa actual | Excel, Word e organização manual. | Mensagens ou memória. | Folha ou caderno de contas. |
| Evidência | Relato do responsável do produto, 2026-10-14. | O mesmo relato. | O mesmo relato, com a regra da validação no mesmo dia. |
| Necessidade | Clientes, planos, packs presenciais e portal com tudo junto. | Email de sessão ao cliente e ao treinador, podendo ser na véspera. Aviso de check-in. | Ao atribuir o pack, fica pendente e o cliente vê o valor a pagar. Depois do comprovativo e da validação, fica activo até as aulas acabarem. Aí os dois são avisados. A faturação só soma na validação. |
| O que continua por validar | Suplemento na refeição ou à parte, com nota. Ver `RN-014`. | Email de sessão cerca de um dia antes, para os dois, e email de mudança da subscrição. Ver `RN-016`. | Momento fechado: depois do go-live. |

## 4. Leitura face ao que o MVP já tem

Isto não muda o relato. Só diz o que já está no produto e o que o relato pede a mais.

| Pedido do relato | No MVP até ao go-live | Nota |
|---|---|---|
| Clientes, plano de treino, alimentação, packs, portal | Já no âmbito. Fechos de sprint até à 6F. | A visão cobre isto. |
| Avaliação inicial | Já no âmbito. | |
| Avaliações periódicas no modelo de cada treinador | São os check-ins que já existem. Na 6GB, o nome na interface passa a «Avaliações» / «Avaliação periódica». A função não muda. O aviso ao cliente é a página de avaliações no portal, sem email. «Métricas» são o peso, as medidas corporais e o questionário que já existem. Os ecrãs já dizem «Avaliações». | Aplicado na 6GB (personal trainer 2026-10-21, cliente 2026-10-22). |
| Suplemento atribuído a cada refeição | O treinador pode pôr o suplemento na refeição. Também pode haver suplemento fora do plano alimentar, com nota (exemplo: cafeína 30 minutos antes do treino). | Escrito em `RN-014`. |
| Email de lembrete de sessão | Antes do go-live, o cliente e o treinador recebem o email, de preferência um dia antes. O ficheiro `session-reminder.html` e o tipo de job existem desde o Sprint 5A. O código que escolhe as sessões da véspera nasce depois da 6GB e antes do Sprint 7. O Sprint 9 só liga o horário. | Falta esse envio. |
| Aviso de subscrição por email | Os cinco modelos chegam: activada, pagamento confirmado, pagamento falhado, fim do trial e cancelamento. Mudar de tier não leva email próprio, por agora. | Nada em aberto neste ponto. |
| Saber quanto fatura | Decidido em 2026-10-14. Entra depois do go-live, quando os sprints já definidos estiverem feitos. Até lá, atribuir o pack activa-o. A regra nova: o valor a pagar é o do pack atribuído; o envio do comprovativo não activa o pack; se o servidor recusar o ficheiro, o cliente vê a razão; o treinador valida à mão, com ou sem comprovativo; sem comprovativo escreve o porquê no histórico dele, não no do cliente; a recusa leva nota no painel do cliente. Hoje `ClientSessionPack.IsUsable` activa o pack na atribuição. | Nada em aberto neste ponto. |
