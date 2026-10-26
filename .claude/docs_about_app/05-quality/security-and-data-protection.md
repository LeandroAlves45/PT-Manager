# Segurança e protecção de dados

Estado: à espera do teu preenchimento (2026-10-14).

Isto não é um parecer jurídico. Categorias especiais (saúde) dependem da finalidade e do contexto. O que não souberes fica «não sei».

Consentimentos formais do cliente estão fora do MVP (`DEF-COMP-001`, Sprint 10C).

| Categoria | Exemplos no produto | Para que serve, no produto | Quem vê | Quanto tempo fica | A tua nota |
|---|---|---|---|---|---|
| Conta | Email, palavra-passe (só hash), papel | Entrar e autorizar | O próprio; o servidor | **por preencher** | **por preencher** |
| Ficha do cliente | Nome, contactos | O trainer identificar o cliente | Trainer desse cliente; o próprio | **por preencher** | **por preencher** |
| Corpo e acompanhamento | Avaliação inicial, peso, check-in | Ajustar o plano | Trainer desse cliente; o próprio no que o portal mostra | **por preencher** | **por preencher** |
| Treino, comida, suplementos | Planos, séries, tomas | Executar o acompanhamento | Trainer e o próprio cliente | **por preencher** | **por preencher** |
| Fotografia | Avatar moderado | Mostrar o cliente na app | **por preencher** | **por preencher** | **por preencher** |
| Marca | Nome, cores, logótipo | O portal e todas as páginas da área do trainer mostrarem a marca dele | Clientes desse trainer; o trainer | **por preencher** | A área do trainer entra na 6GB-2. Decisão de 2026-10-15. |
| Subscrição | Plano, limite, identificadores Stripe | Cobrar a plataforma ao trainer | O trainer; a operação de billing | **por preencher** | **por preencher** |
| Comprovativo (futuro) | Ficheiro que o cliente enviaria | O trainer confirmar um pagamento feito fora da app | Ainda sem regra | — | Fora do MVP |

## Serviços externos que recebem dados

Stripe (subscrição), Cloudinary (imagens), fornecedor de vídeo privado (R2, Sprint 5D), email transacional (Resend) e, a partir do Sprint 7A, Sentry (erros) e Grafana Cloud (traces e métricas). Não listar chaves nem contas.

O Sentry e o Grafana ficam na região UE. Não recebem emails, nomes, ids de pessoas na rota, query, cookies, headers, corpo dos pedidos, payload Stripe nem scores de moderação. Isto está provado por testes no Sprint 7A (`QG7A-REDACT-001`, `QG7A-PII-001`). Os logs com ids ficam só na consola do Render.

Há mais algum? **A tua resposta:** por preencher

## O que queres poder fazer com os dados de uma pessoa

Exportar, corrigir e apagar: **por preencher**

## Incidente

Quem avisas e em quanto tempo: **por preencher**
