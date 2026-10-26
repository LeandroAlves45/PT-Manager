# Visão do produto

Estado do documento: rascunho de 2026-10-14. Duas decisões vieram do responsável do produto neste dia. O resto está reconstruído a partir de `.claude/project/00_ARCHITECTURE.md`, `.claude/project/02_SPRINTS_ROADMAP.md` e `.claude/memory/ACTIVE.md`. Onde essa reconstrução ainda precisa de um sim teu, fica `Por validar`.

## 1. Resumo

O PT Manager é um serviço na web para um personal trainer tratar, num só sítio, das pessoas que treina, dos planos, das sessões, dos packs e da sua própria subscrição. Cada cliente entra numa área com a marca desse treinador: nome, cores e logótipo. Ao guardar o logótipo ou a marca, muda em todas as páginas da área do treinador, não só na dashboard. Entra na 6GB-2. Decisão de 2026-10-15.

Há três papéis no sistema: `trainer`, `client` e `superuser`. Os dados de um treinador não são visíveis a outro treinador.

## 2. Problema

Decisão de 2026-10-14: o treinador junta clientes, planos, sessões e packs num só sítio, e o cliente acompanha o trabalho com a marca desse treinador.

Check-in, na linguagem do produto, é a avaliação periódica. Desde a 6GB, o nome na interface é «Avaliações» ou «Avaliação periódica». A função não muda. Os dados são o peso, as medidas corporais e o questionário que a avaliação inicial e cada check-in já guardam. Os ecrãs actuais ainda dizem check-in.

Pagamento, na mesma decisão: o treinador cria o pack do cliente. O cliente não paga ao treinador dentro da app.

Faturação e pack, decisão complementar do mesmo dia: o treinador cria o tipo de pack e atribui-o ao cliente. Nesse momento o pack fica pendente. O cliente vê, no seu painel, o pack e o valor a pagar, e pode receber o mesmo aviso por email. Paga fora da app e envia o comprovativo. O envio, por si, não activa o pack.

Se o servidor recusar o ficheiro, a app mostra ao cliente a razão. O pack continua pendente.

O treinador pode validar à mão, mesmo sem comprovativo. Nesse caso tem de escrever o porquê. Essa nota fica no histórico do treinador, não no do cliente. O cliente não é informado de que o envio é opcional, salvo se o próprio treinador lho disser. Só a validação do treinador activa o pack e soma o valor a pagar na faturação do painel (número de clientes, mês corrente e meses desde a inscrição). O pack activo dura até as aulas terminarem. Aí o treinador e o cliente são avisados e o processo pode repetir-se com um pack novo.

O treinador pode recusar. Tem de escrever a razão. O cliente vê essa nota no painel dele. O pack não fica activo.

O código actual não faz isto. Em `ClientSessionPack`, atribuir o pack deixa-o utilizável de imediato se tiver sessões e não estiver apagado. O preço fica gravado nesse pack, copiado do tipo. O pack completa quando o saldo de sessões chega a zero. A data prevista não o desactiva.

Decisão de momento, no mesmo dia: esta regra fica para depois do go-live, quando os sprints já definidos estiverem feitos. Até lá, atribuir o pack continua a activá-lo, como o código faz hoje. A faturação por comprovativo não entra no MVP.

Isto é a frase do produto. Não há, neste repositório, entrevistas ou medições com treinadores que provem a frase. O benchmark de 2026-09-15 (`.claude/project/frontend/04_BENCHMARK_E_FUNCIONALIDADES.md`) só mostra o que produtos parecidos anunciam. Não conta como prova de que os clientes do PT Manager têm este problema.

## 3. Para quem

| Quem | Papel nesta visão | Estado |
|---|---|---|
| Personal trainer | Utilizador principal. Gere clientes, planos, sessões, packs, marca e a própria subscrição. | Confirmado pela decisão de 2026-10-14 e pelos papéis da arquitectura. |
| Cliente | Usa o portal do seu treinador: treino, nutrição, suplementos, avaliações periódicas e perfil. | Confirmado na mesma decisão. O portal está fechado no Sprint 6F. O nome «check-in» muda na 6GB. |
| Superuser | Trata dos catálogos globais, da moderação e do painel de treinadores e da faturação da plataforma. Não é o utilizador para quem o produto existe. | Presente na arquitectura e no Sprint 6D. O painel de treinadores e o bloqueio de treinadores entram antes do go-live. |
| Outras pessoas afectadas | Não há mais ninguém. | Confirmado em 2026-10-14. |

## 4. O que o MVP tem de conseguir

1. O treinador trabalha os seus clientes sem misturar dados com os de outro treinador.
2. O treinador convida o cliente, marca sessões, controla packs e deixa planos de treino, alimentação e suplementos.
3. O cliente vê e regista o que é seu, na marca do treinador.
4. O treinador subscreve a plataforma e o sistema respeita o limite de clientes do plano.
5. A plataforma distingue o que cada papel pode fazer e recusa o resto no servidor.

O tecto assumido na arquitectura é cerca de 100 trainers. Não há dados de produção antigos para trazer.

## 5. Âmbito e estado

O âmbito deste documento é o MVP até ao go-live (Sprints 0 a 9). O Sprint 10 fica de fora. Decisão de âmbito: 2026-10-14.

| Capacidade | Estado | Onde está registado |
|---|---|---|
| Contas, papéis e isolamento entre trainers | Implementado | Arquitectura e fechos até ao Sprint 6F |
| Clientes, convite, avaliação inicial, sessões, packs | Implementado | Sprint 6E, fecho no repositório |
| Planos de treino, alimentação, suplementos, check-ins, marca e subscrição do trainer | Implementado | Sprints 5B, 5C e 6E |
| Portal do cliente: treino de hoje, nutrição, tomas, check-ins, perfil e fotografia moderada | Implementado | Sprint 6F, fechado em 2026-10-14 |
| Catálogos globais e moderação | Implementado | Sprint 6D |
| Uso real com login, nos gates manuais ainda abertos | Por validar | `ACTIVE.md`: gates `QG6E*`, `QG6F*` e `QG6D-ADMIN-001` |
| Entrar, criar conta, confirmar email, recuperar password, convite e Google, nos três papéis | Planeado | Sprint 6GA: blueprints validados em 2026-10-14, por aplicar |
| Correcção visual dos ecrãs já construídos, incluindo o nome «Avaliações», os planos em página, o perfil do cliente, a lista de planos alimentares, a nota de conclusão do treino para o treinador e o filtro de dia para ler treinos anteriores | Planeado | Sprint 6GB |
| Email da sessão na véspera, bloqueio de suplementos e treinadores, arquivo completo do cliente e avaliação inicial preenchida pelo cliente | Planeado | Logo a seguir à 6GB e antes do Sprint 7. Ordem: avaliação inicial pelo cliente, email da sessão, arquivo do cliente, e no fim o bloqueio (com a contagem de contas bloqueadas no painel). O Sprint 9 só liga o horário do email |
| Painel do superuser (sem a contagem de bloqueados), isenção de pagamento e convite isento | Implementado na 6GB-SU (2026-10-19) | Decisões U39–U52 em `sprint_6GB/00_plano_6GB_superuser.md` |
| Logs, erros e métricas de operação | Blueprints validados, por aplicar (2026-10-25) | Sprint 7A: Sentry e Grafana Cloud, região UE, seis alertas por email |
| Cache e limites partilhados entre instâncias | Planeado como decisão | Sprint 7B: só se implementa se a medição o justificar. A medição do 7A mostrou uma só instância e três leituras com muitas queries |
| Suite crítica e entrega contínua | Planeado | Sprint 8 |
| Serviço no ar para utilizadores reais | Planeado | Sprint 9: Render, Vercel, Neon, QStash |

`Implementado` aqui significa fecho de sprint registado, com testes citados em `ACTIVE.md`. Não significa que um gate manual com password já tenha sido feito.

## 6. Fora do âmbito do MVP

Fica para o Sprint 10, ou está excluído. A lista completa de entradas está no roadmap (`DEF-*`).

- Chat, hábitos, fotos de progresso, cursos, cofre de ficheiros, staff e automações.
- O cliente cancelar uma sessão.
- Notificações dentro da app e notificações push. Email transacional existe.
- Métricas corporais à escolha do trainer, histórico de versões dos planos e relatórios guardados.
- Importar alimentos por CSV e uma pesquisa única no servidor para o comando de pesquisa.
- Pagamento do cliente ao treinador dentro da app. O trainer cria o pack; não há transação. O comprovativo, o estado pendente e a faturação do painel entram depois do go-live, quando os sprints já definidos estiverem feitos. Até lá, atribuir o pack activa-o.
- Consentimentos formais do cliente. Entram só depois de análise de produto e jurídica.
- Categoria de alimento, e notas de fonte no alimento global. Excluídos em 2026-09-16, não são trabalho futuro.

## 7. Limitações já aceites

- Lembretes podem atrasar cerca de vinte minutos, mais o arranque a frio, no plano gratuito. Está aceite para o MVP.
- A cache partilhada não é obrigatória no go-live. O Sprint 7B pode adiá-la.
- Sem aviso automático quando a API está em baixo: uma sonda manteria o Render e o Neon acordados no plano gratuito. Fica para quando houver plano pago (Sprint 9/10).
- A moderação automática de vídeo e a revisão humana em fila larga não entram no MVP.
- O go-live estimado no índice do projecto era Outubro de 2026. Em 2026-10-14 os Sprints 6G, 7, 8 e 9 continuam por concluir. Essa data não é um compromisso fechado neste documento.

## 8. Próximo passo do produto

Estado em 2026-10-25: o bloco pré-Sprint 7 está na `main`. Os blueprints do Sprint 7A (observabilidade) estão validados e por aplicar. O próximo passo é o Leandro aplicá-los e criar as contas Sentry e Grafana na região UE. Depois vêm os alertas e, por fim, o Gate 7B, que decide a cache com a medição do 7A (`docs/blueprints/sprint_7/sprint_7A/08_carga_k6_e_medicao.md`). A seguir vêm o Sprint 8 (testes e CI) e o Sprint 9 (publicação, com o horário do email da véspera). Os gates manuais com login da 6D, 6E e 6F continuam abertos como risco (`R-001`).

## 9. O que falta preencher noutros ficheiros

1. Actores fechados: só personal trainer, cliente e superuser.
2. Evidência de utilizadores reais, se existir: `01-product/problem-analysis.md`. A frase do problema, até lá, é uma decisão tua.
3. Faturação e pack: a regra está na secção 2 e entra depois do go-live, quando os sprints já definidos estiverem feitos. Até lá, atribuir o pack activa-o.
