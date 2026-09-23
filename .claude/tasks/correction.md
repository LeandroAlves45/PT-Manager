# Correction Patterns

Padrões de erro identificados depois de correções do utilizador.

## 2026-09-20 — Classificar um teste intermitente como "flakiness conhecida" em vez de investigar

**O que aconteceu.** Ao fechar a Sprint 6B, sete testes
`*_WithoutToken_ReturnsUnauthorized` falharam com 429 numa corrida integral. Verifiquei que
os ficheiros estavam intocados pela sprint e que passavam em isolamento, concluí que era
flakiness pré-existente do rate limiter, documentei-a como tal e dei a sprint por fechada.
O utilizador correu a suite no terminal, teve 6 falhas do mesmo tipo, e pediu explicação.

**O que estava errado.** Havia uma causa raiz concreta e corrigível: o `TestServer` do
`WebApplicationFactory` não preenche `RemoteIpAddress`, por isso o `IpKey` do limitador
global caía sempre em `"ip:unknown"` e toda a suite partilhava um orçamento de 60 pedidos
anónimos por minuto. Nada de aleatório — um recurso partilhado com orçamento, mais
paralelismo. A correção foram 40 linhas no host de teste, sem tocar em produção.

**O padrão.** "Passa em isolamento, falha em conjunto" e "o conjunto de testes afetados muda
a cada corrida" não são sinais de ruído: são a assinatura de um recurso partilhado com
limite. `NAO` registar como flakiness conhecida sem antes procurar o recurso partilhado.

**A regra.** Perante um teste intermitente:
1. Identificar o que os testes afetados têm em comum (aqui: pedido anónimo ao host completo).
2. Procurar estado partilhado entre eles — contadores, orçamentos, caches, partições, ligações.
3. Só depois de encontrar o recurso e provar que não é controlável é que se classifica como
   flakiness. E mesmo aí, dizê-lo ao utilizador em vez de o enterrar num relatório.

Nunca fechar uma sprint como "tudo verde" tendo visto falhas numa corrida integral, mesmo que
outra corrida passe.

## 2026-09-23 — Testes do frontend vivem em `frontend/src/test/`, não ao lado do código

**O que aconteceu.** No fecho da 6C comecei a criar `session.test.ts` e `problem.test.ts` ao
lado dos ficheiros de produção (`src/shared/api/`), como indica o doc 11. O utilizador corrigiu:
os testes ficam todos em `frontend/src/test/`, sem misturar com o código.

**A regra.** Testes frontend em `frontend/src/test/`, espelhando o caminho do ficheiro testado
(ex.: `src/shared/api/client.ts` → `src/test/shared/api/client.test.ts`). Infra (MSW, setup,
render) também em `src/test/`. Os caminhos do doc 11 ficam registados como desvio aprovado.
