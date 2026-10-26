# ADR-001: destino da telemetria

- Data: 2026-10-25
- Estado: aceite (Sprint 7A). Os blueprints estão validados e a aplicação está por fazer.
- Contexto:
  - A arquitectura (§12.2) e o roadmap diziam que o exporter e a retenção tinham sido «confirmados
    no Sprint 0». Não havia registo dessa decisão.
  - O Sprint 7A precisava de erros, traces, métricas e alertas, no plano gratuito.
  - O Render dorme quando não há tráfego, e o Neon também.
- Decisão:
  - **Erros** vão para o Sentry Developer (gratuito, 5k erros/mês, 30 dias). Só erros
    (`TracesSampleRate = 0`), sem dados pessoais, sem corpo e sem headers.
  - **Traces e métricas** vão para o Grafana Cloud Free (10k séries, 50 GB de traces, 14 dias). O
    envio é por OTLP http/protobuf, configurado pelas variáveis standard `OTEL_EXPORTER_OTLP_*`.
  - **Logs** ficam só na consola do Render, em JSON, com trace id e correlation id.
  - As duas contas ficam na **região UE**.
  - Os **alertas** vão por email: um no Sentry (erro novo) e cinco no Grafana.
  - Não há sonda de uptime.
- Alternativas:
  - **Só Sentry.** O OTLP do Sentry não aceita métricas e está em beta. Sem métricas, o Gate 7A
    não fecha.
  - **Honeycomb + Sentry.** São só 2 alertas grátis.
  - **Logs também no Grafana.** É mais um sítio para onde dados podem sair, sem necessidade
    provada.
  - **Sonda de uptime.** Mantinha o Render e o Neon acordados e gastava as horas grátis.
- Consequências:
  - São duas contas a gerir, e os segredos ficam só em variáveis de ambiente.
  - Os nomes das métricas no Grafana confirmam-se no gate manual `QG7A-ALERT-002`.
  - O contexto de trace que chega nos pedidos é ignorado (S7-13). Quando o frontend quiser enviar
    `traceparent`, esta decisão revê-se.
  - Detalhe completo: `docs/blueprints/sprint_7/00_plano_sprint_7.md` e o pack `sprint_7A/`.
