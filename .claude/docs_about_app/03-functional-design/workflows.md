# Fluxos de trabalho

Estado: à espera do teu preenchimento (2026-10-14).

Só entram processos com decisões ou mais do que um actor. Um fluxo simples fica no caso de uso e não precisa de diagrama.

Para cada fluxo, escreve os passos por ordem. Se houver uma decisão, escreve a pergunta e os dois caminhos. Na revisão eu passo isso a diagrama, se ajudar.

## FL-001 — Pack e sessão

Liga a `UC-003`, `UC-004`, `RN-002`, `RN-003`, `RN-011`, `RN-019`.

Passos do pack, confirmados em 2026-10-14:

1. Até ao go-live, o treinador atribui o pack e ele fica activo. O cliente não o vê.
2. Depois do go-live, o treinador atribui e o pack fica pendente, com o preço que ele definiu.
3. O cliente vê o valor e pode receber email. Paga fora da app e envia o comprovativo. O envio não activa o pack.
4. Se o ficheiro for recusado, o cliente vê a razão e o pack continua pendente.
5. O treinador valida, com ou sem comprovativo. Sem comprovativo, a nota fica no histórico dele, não no do cliente. A app não diz ao cliente que o envio é opcional.
6. Se recusar, a nota aparece no painel do cliente.
7. Só a validação activa o pack e soma o valor.
8. Quando as aulas acabam, os dois são avisados. Um pack novo repete o fluxo.

Passos da sessão, confirmados em 2026-10-14:

1. O treinador marca a sessão. Não pode ter duas à mesma hora de início. O cliente não pode ter duas no mesmo dia.
2. O cliente não vê a lista e não cancela pela app.
3. O cliente e o treinador recebem um email por sessão na véspera, no fuso do treinador. Só se a sessão for no dia seguinte. Sessão para hoje, já passada ou cancelada antes do envio não recebe este email. A mesma sessão, na mesma data, não volta a ser enviada. Se mudar para outro dia futuro, a nova véspera manda outro. Se passar para hoje, não. Enquanto o treinador está bloqueado, este email não sai. As sessões ficam como estão. Quando o acesso volta, a véspera volta a aplicar-se às que ainda sejam no dia seguinte. Esse envio nasce depois da 6GB e antes do Sprint 7. O Sprint 9 só liga o horário.
4. Cancelar não gasta o pack. Concluir ou falta gasta.
5. Se o treinador repuser uma sessão concluída ou uma falta, o saldo volta, se ainda couber.

## FL-002 — Convite até ao primeiro acesso do cliente

Liga a `UC-002`, `RF-003`, `RF-015`, `FL-006`.

Passos, confirmados em 2026-10-14:

1. O treinador cria o cliente com o email e envia o convite.
2. O cliente abre o convite e entra no portal.
3. A forma exacta de entrar fica na autenticação da 6GA. Se o link já expirou, a pessoa vê que o convite expirou e que tem de pedir outro ao treinador. Não cria a conta. Se o link já foi usado, vê «Este convite já foi usado.» Não cria outra. Um link que não seja um convite válido diz «O convite não é válido.» Não revela se o email existe. As duas mensagens entram na 6GA. Enviar outro convite anula o anterior. O link antigo diz «O convite não é válido.» Há um botão «Copiar link». O link não fica sempre à vista. Entra na 6GB-2. As frases do link entram na 6GA. Na ficha do cliente, o treinador vê que o convite foi enviado e até que dia e hora é válido, no seu fuso. Os 7 dias contam desde o envio. Essa data na ficha entra na 6GB-2, não na 6GA. Se os 7 dias passarem sem o cliente aceitar, a ficha mostra que o convite expirou, com essa data, e o treinador pode enviar outro. Depois de o cliente aceitar, a ficha deixa de mostrar o convite. Se o treinador mudar o email da ficha antes de o convite ser aceite, o link antigo continua válido para o email antigo. O pack da 6GA não se altera. Na ficha, na 6GB-2, mostra-se o convite do email antigo, com a data, para se ver que não coincide com o email actual. No convite do treinador isento, a validade e a anulação seguem o mesmo. A ficha desse convite fica no bloco da isenção, não na 6GB. O link já usado desse convite diz que entra com a conta que criou. O link inválido, e o antigo, dizem só que o convite não serve. Se o cliente for arquivado antes de usar o convite, o link deixa de servir. Reativar não recupera esse link. O treinador envia outro, se precisar. Anular o convite entra no bloco, com o resto do arquivo.
4. A partir daí segue a avaliação inicial (`FL-006`), se ainda não existir.

Aplicado na 6GB, fase do personal trainer (2026-10-21): ao enviar o convite, o personal trainer vê «Copiar link» e a validade; fechado o aviso, o link não volta a aparecer. Se o email falhar, o convite fica criado e o aviso diz que o email não saiu e que o link deve ir por outro canal. A ficha mostra «Convite enviado · válido até…», «O convite expirou a…» com «Enviar outro convite», o email do convite quando é diferente do email da ficha, e nada depois de aceite.

## FL-003 — Treino de hoje

Liga a `UC-005`, `RF-007`, `RN-007`, `RN-015`.

Passos, confirmados em 2026-10-14:

1. Há um plano de treino activo por cliente.
2. O cliente vê e faz o treino de hoje numa página dedicada do portal. O menu e o cartão da dashboard levam a essa página. As séries, as cargas e o resto só se registam na página. No cartão não há edições.
3. Pode corrigir ou apagar o registo do que fez numa série (carga, repetições e nota), só no treino de hoje, no fuso do treinador. Não apaga a série escrita no plano. Concluir o treino também é só nesse dia, e só o cliente o faz. A conclusão não se anula. Pode concluir mesmo que faltem séries por registar. Depois de concluir, ainda pode registar e corrigir séries nesse dia. Não pode apagar. A conclusão mantém-se. No plano, a série pode ter mínimo e máximo de repetições. O máximo é opcional. Se só houver um número, esse é o alvo. Com os dois, o intervalo escreve-se com o maior primeiro, como 12-10. O cliente regista um só número do que fez, mesmo fora do intervalo. A app aceita e não avisa. O intervalo é o alvo. Entra na 6GB-2. O treinador vê o que foi registado e a conclusão, e pode corrigir séries mesmo depois de o cliente concluir. Não as apaga. O print da 6GB que pedia só leitura não altera isto. Decisão de 2026-10-15. O treinador vê-a e não a marca. A nota pode ficar em branco. Se for escrita, fica gravada ao concluir e depois não se edita. O treinador passa a vê-la na 6GB. Hoje os ecrãs do treinador mostram as notas das séries, não esta nota. Dias anteriores, antes do go-live, o cliente lê noutro dia por um filtro, para comparar cargas e repetições. Também lê, só a ler, as notas das séries e a nota da conclusão, se existir. Cada série mostra a carga e as repetições registadas, ou fica vazia se não houve registo. Ao lado vê o que o plano pedia nessa série. Não regista, não corrige e não apaga. Essa página hoje só tem o dia de hoje. O filtro entra na 6GB. A página abre no dia de hoje, que se edita. O filtro abre um dia anterior do plano activo, só a ler. Voltar a hoje repõe a edição. Não guarda o dia anterior como página de entrada. Mostra só dias já passados do plano activo. Não mostra dias futuros. Um plano arquivado não entra. O treinador é que vê os planos antigos. O treinador vê esses registos e pode corrigi-los, também em dias anteriores. Não apaga registos. Se o cliente apagar o registo de hoje, o treinador pode criar outro registo dessa série. Não há desfazer. A série escrita no plano não se mexe.
4. Não altera o plano.
5. O treinador vê e avalia essas notas.

Aplicado na 6GB, fase do personal trainer (2026-10-21): o intervalo de repetições («12-10») já está no plano e no portal; o personal trainer vê as séries realizadas agrupadas por dia (no seu fuso) e por exercício, com o planeado ao lado, e a nota de conclusão do cliente, só de leitura; continua a corrigir séries numa página própria e não as apaga (decisão confirmada a 2026-10-21). O filtro de dia do cliente fica para a fase do cliente.

Os cartões da dashboard ficam. Abrem a página e não deixam editar. O do treino não regista séries. O menu «Treino de hoje» também leva à página.

## FL-004 — Check-in, da marcação à revisão

Liga a `UC-006`, `RN-009`, `RN-020`.

Passos, descritos em 2026-10-14:

1. O cliente preenche a avaliação inicial.
2. Só depois o treinador pode marcar uma avaliação periódica.
3. O treinador cria-a. O cliente responde no dia.
4. O cliente vê a confirmação e não fica com o histórico. Não há email antes do go-live.
5. O treinador vê-a em «Check-ins por rever» e marca-a como revista. Se o cliente for arquivado, sai dessa lista. Os dados ficam. Se for reativado e ainda não estiver revista, volta à lista.
6. O treinador pode marcar a próxima antes de o cliente responder à actual. O portal já mostra essa próxima. O ritmo habitual é marcá-la depois da resposta.
7. Se o dia passar sem resposta, essa avaliação fecha. O cliente não a responde depois. O treinador marca outra. A que falhou fica com o treinador, sem resposta. O cliente não marca avaliações.

O nome na interface muda na 6GB.

## FL-005 — Subscrição e limite de clientes

Liga a `UC-007`, `RN-008`, `RN-018`.

Passos, confirmados em 2026-10-14:

1. Quem cria a própria conta começa no Free, com trial, e paga se mudar de plano.
2. Se a subscrição ficar inactiva, suspensa por pagamento ou cancelada, o treinador e os clientes continuam a entrar. O treinador não cria nem reactiva clientes até voltar a ter subscrição activa.
3. O superuser pode isentar quem já está na app, sem email. Essa pessoa deixa de pagar e fica no Pro.
4. Pode tirar a isenção. O Pro gratuito acaba. Sem subscrição activa, o treinador não cria clientes.
5. Para um email que ainda não é treinador, o superuser envia um convite de uso único, válido 7 dias. A pessoa escolhe a password e a conta nasce já no Pro isento, sem pagamento. Este convite não aceita Google. Não cria um Free nem um Pro isento por Google. Diz que o convite é com password. Link expirado: o superuser envia outro e não se cria conta. Link já usado: entra com a conta e não cria outra. O superuser vê que o convite foi enviado e até que dia e hora é válido, em Europe/Lisbon. Os 7 dias contam desde o envio. Estas frases entram no bloco, com a isenção.
6. Se o email já tiver conta, o convite é recusado. O superuser isenta essa conta, sem email.
7. No painel, os isentos são um número à parte. Não contam como subscrição activa paga e não entram no dinheiro deste mês.

A isenção e o convite foram antecipados para a 6GB-SU (2026-10-19) e estão na app, na página «Treinadores» do superuser. O convite aceita-se em `/convite-treinador`. O limite e o acesso já estão no Sprint 5B.

## FL-006 — Avaliação inicial até poder planear

Liga a `UC-008`, `RF-004`, `RN-020`.

Passos, confirmados em 2026-10-14:

1. O treinador cria o cliente com o email.
2. O cliente entra no portal.
3. Se ainda não houver avaliação inicial, vê um cartão com o formulário no início do portal e pode ir às outras páginas.
4. Sem essa avaliação gravada, não há plano de treino, plano alimentar, suplementos nem avaliação periódica. Nessas páginas a app explica que ainda falta a avaliação. Sessões e packs podem existir.
5. O cliente preenche os mesmos campos de hoje. O formulário desaparece. Não volta a ver a avaliação.
6. O treinador vê-a na dashboard para rever. Não há email. Se o cliente for arquivado antes disso, sai da lista. Os dados ficam. Se for reativado e ainda não estiver revista, volta à lista.
7. A partir da gravação, o treinador pode criar os planos. Marcar como revista só tira a avaliação da lista. Pode corrigir os dados ou deixá-los como o cliente gravou.
8. Se o treinador já criou a avaliação, o cliente não a preenche.

## FL-007 — Arquivar e reativar um cliente

Liga a `UC-001`, `RN-005`, `RN-006`.

Passos, confirmados em 2026-10-14:

1. O treinador arquiva o cliente.
2. As sessões futuras são canceladas. Os planos saem da vista activa. O saldo do pack que resta é consumido. O lugar na subscrição fica livre.
3. O cliente deixa de entrar nesse momento. Não há email. A app não diz que a conta foi arquivada. A entrada falha sem explicar. Uma sessão já aberta deixa de servir nesse momento.
4. Reativar só ocupa lugar se houver, mesmo que a pessoa nunca tenha aceite um convite. Não devolve planos nem saldo. Quem já tinha conta volta a entrar, sem convite novo. A sessão que foi cortada não volta. Não há email de reativação. Quem nunca aceitou precisa de um convite novo.
5. O treinador dono vê sessões feitas e planos antigos em leitura. Outro treinador não os vê. O cliente não os leva. Um convite ainda não usado deixa de servir. Reativar não o recupera.

O limite ao reativar já está na app. O resto entra no bloco depois da 6GB e antes do Sprint 7.

## FL-008 — Bloquear um treinador

Liga a `RN-013`.

Passos, confirmados em 2026-10-14:

1. O superuser bloqueia com um dos quatro motivos e pode acrescentar uma nota.
2. O treinador deixa de entrar nesse momento e recebe email com o motivo, a nota e que os clientes ainda entram até àquela hora. Se não houver clientes, o email só traz o motivo e a nota. Ao tentar entrar, a app mostra o mesmo motivo e a mesma nota. Uma sessão já aberta deixa de servir nesse momento.
3. Os clientes recebem email e um aviso no início do portal, e continuam a usar o portal durante 7 dias. Não vêem o motivo nem a nota. O aviso diz o dia e a hora em que o acesso acaba, no fuso do treinador. Os 7 dias contam desde o momento do bloqueio. As outras páginas continuam a funcionar. Quando o bloqueio é levantado, o aviso sai.
4. Se o bloqueio continuar, no fim dos 7 dias também deixam de entrar. A app diz que o prazo de acesso acabou, sem motivo nem nota. Uma sessão já aberta acaba nesse momento.
5. Os dados não se apagam e ficam desse treinador.
6. Quando o bloqueio é levantado, esteja o prazo a meio ou já passado, o acesso volta. Os dois recebem email. A sessão que foi cortada não volta. Entram outra vez.

Isto entra no bloco depois da 6GB e antes do Sprint 7. Hoje só alimentos e exercícios têm bloqueio. Bloquear um alimento, um exercício ou um suplemento não tira o acesso, não tem os 7 dias e não envia email. O treinador vê o motivo e a nota no item. Se o item já estiver num plano, sai do portal do cliente. No plano do treinador fica visível como bloqueado, para o treinador o substituir. Desbloquear não o repõe no plano do cliente. Sai do plano e fica na biblioteca, outra vez utilizável. O treinador volta a pô-lo se quiser.

## FL-009 — **por preencher**
