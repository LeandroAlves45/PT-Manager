# Casos de uso

Estado: à espera do teu preenchimento (2026-10-14).

Cada caso liga a um `RF`. O fluxo principal fica em branco de propósito: escreve os passos como o actor os vive, não como a API está feita. Na revisão eu junto isso ao comportamento real.

Copia o bloco vazio para um caso novo e usa o próximo `UC-`.

## UC-001 — Gerir clientes

- Objectivo: o trainer mantém a lista dos seus clientes.
- Actor principal: trainer.
- Requisitos: `RF-002`, `RF-001`. Regras: `RN-005`, `RN-001`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado no fecho da 6E. Gate manual com login por validar.

## UC-002 — Convidar e entrar como cliente

- Objectivo: o cliente passa a aceder ao portal do seu trainer.
- Actor principal: client. Secundário: trainer.
- Requisitos: `RF-003`, `RF-015`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: convite Implementado; auth final Planeado (6GA).

## UC-003 — Atribuir pack

- Objectivo: o trainer cria o pack do cliente, sem pagamento dentro da app.
- Actor principal: trainer.
- Requisitos: `RF-006`. Regras: `RN-002`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado no fecho da 6E.

## UC-004 — Agendar e concluir sessão

- Objectivo: o trainer marca a sessão e fecha-a.
- Actor principal: trainer.
- Requisitos: `RF-005`. Regras: `RN-002`, `RN-003`, `RN-004`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado no fecho da 6E. Cancelamento pelo cliente está fora do MVP.

## UC-005 — Fazer o treino de hoje

- Objectivo: o cliente regista as séries e conclui o treino.
- Actor principal: client.
- Requisitos: `RF-007`. Regras: `RN-007`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado no fecho da 6F.

## UC-006 — Check-in

- Objectivo: o trainer pede o ponto de situação e o cliente responde.
- Actores: trainer e client.
- Requisitos: `RF-010`. Regras: `RN-009`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado nos fechos da 6E e da 6F.

## UC-007 — Subscrever a plataforma

- Objectivo: o trainer activa o plano e fica sujeito ao limite de clientes.
- Actor principal: trainer.
- Requisitos: `RF-012`. Regras: `RN-008`.
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: Implementado no Sprint 5B / 6E.

## UC-008 — Preencher a avaliação inicial

- Objectivo: o cliente entrega a avaliação inicial e o treinador passa a poder planear.
- Actor principal: client. Secundário: trainer.
- Requisitos: `RF-004`. Regras: `RN-020`. Fluxo: `FL-006`.
- Pré-condições: o cliente já entrou no portal e ainda não há avaliação inicial.
- Fluxo principal: o de `FL-006`.
- Alternativa: o treinador cria a avaliação antes. O cliente não a preenche.
- Erro: uma segunda avaliação inicial do mesmo cliente é recusada.
- Pós-condições: a avaliação fica do treinador, por rever. Os planos já podem ser criados.
- Estado: o registo pelo trainer está implementado. O preenchimento pelo client entrou no bloco pré-Sprint 7 (2026-10-24).

## UC-009 — **por preencher**

- Objectivo: **por preencher**
- Actor principal: **por preencher**
- Requisitos: **por preencher**
- Pré-condições: **por preencher**
- Fluxo principal: **por preencher**
- Alternativa: **por preencher**
- Erro: **por preencher**
- Pós-condições: **por preencher**
- Estado: **por preencher**
