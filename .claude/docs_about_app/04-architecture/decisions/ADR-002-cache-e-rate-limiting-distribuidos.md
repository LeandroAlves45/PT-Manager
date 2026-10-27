# ADR-002: cache e rate limiting distribuídos (Gate 7B)

- Data: 2026-10-26
- Estado: aceite (Sprint 7B). Reavaliar no Sprint 10B, com tráfego real.
- Contexto:
  - O Sprint 7B tinha de decidir `Implementar` ou `Não implementar ainda` para o HybridCache, o
    Upstash Redis e o rate limiting distribuído (arquitectura §8, plano do Sprint 7 §5).
  - A medição do 7A mostrou três leituras acima do orçamento de 5 queries: `dashboard` com 18,
    `portal/home` e `clients/{id}/summary` com 9. A latência ficou dentro dos 300 ms.
  - O 7B repetiu a carga k6 e mediu quatro versões do dashboard, com e sem +5 ms de rede
    (toxiproxy). Detalhe em `docs/blueprints/backend-files/sprint_7/sprint_7B/01_decisao_gate_7B.md`.
- Decisão:
  - **Não implementar ainda** o HybridCache, o Upstash Redis nem o rate limiting distribuído.
    O Redis fica fora da equação: o Sprint 9 não o configura.
  - Os sinais 1 (várias instâncias) e 3 (limite local insuficiente) estão ausentes: há uma só
    instância, e o rate limiting local é exato.
  - O sinal 2 aparece só nas queries.
  - O sinal 4 (consumidor verificável) é frágil. O dashboard tem ~30 caminhos de escrita a
    invalidar: 6 passam ao lado do `SaveChanges`, e um muda os dados de outro personal trainer.
  - A consolidação do dashboard numa query SQL (18 → 3 comandos) também **não** se faz agora. Ia
    tirar a segunda barreira de tenant (os Global Query Filters) para ganhar ~35 ms, que o
    orçamento de 300 ms não exige.
- Alternativas:
  - **HybridCache + Upstash Redis (medido).** Os hits saem rápidos, mas:
    - num miss, um Redis em baixo custa ~1,5 s e dois erros no Sentry;
    - o L2 não sobrevive a um cold start com TTL de 30 s;
    - em repouso, o Redis gasta ~86 mil comandos/mês.
  - **Cache só em memória.** Com uma instância, os dados ficam desatualizados até 30 s, e a
    invalidação é igualmente frágil, para um ganho que não é preciso.
  - **SQL único (variante B).** Ver acima, e o doc 01 §14.
  - **LINQ juntado (variante A).** 18 → 11 comandos, e mantém as duas barreiras. Fica como
    primeira opção se o 10B mostrar latência real acima do orçamento.
- Consequências:
  - O dashboard continua com 18 queries; `portal/home` e `summary` continuam com 9.
  - Reavaliar no 10B com o mesmo `backend/tests/Load/read-paths.k6.js` contra o Render + Neon
    reais, medindo também o `SELECT 1` Render → Neon.
  - O custo de tirar o cold start (Render pago, Neon Launch) está no doc 01 §15. É uma decisão
    em aberto do Leandro.
  - Na mesma fase entraram três ajustes, aplicados na `main` em `1898e70`:
    - .NET 10.0.14 (segurança);
    - OTLP fixado em http/protobuf no código (sem isso, nada chegava ao Grafana);
    - logs de SQL a `Warning` em produção.
