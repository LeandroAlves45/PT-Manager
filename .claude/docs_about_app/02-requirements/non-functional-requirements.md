# Requisitos não funcionais

Estado: à espera do teu preenchimento (2026-10-14).

Não inventes números. Se não tiveres uma meta, escreve «sem meta numérica; vale o que a arquitectura já aceitou».

| ID | Área | O que já está escrito na arquitectura ou no roadmap | A tua meta ou correcção |
|---|---|---|---|
| RNF-001 | Acesso | O servidor recusa dados de outro tenant e acções de um papel sem permissão. A interface esconder um botão não chega. | **por preencher** |
| RNF-002 | Sessão | Access token JWT curto, em memória no browser. Refresh opaco, rotativo, em cookie `HttpOnly`. | **por preencher** |
| RNF-003 | Privacidade | Há dados de conta, avaliação, peso, check-in e fotografia. A base jurídica e a retenção não estão decididas no MVP (consentimentos: Sprint 10C, `DEF-COMP-001`). | **por preencher** |
| RNF-004 | Capacidade | Tecto de produto assumido: cerca de 100 trainers. | **por preencher** |
| RNF-005 | Atrasos | Lembretes podem atrasar cerca de vinte minutos, mais o arranque a frio, no plano gratuito. Aceite no MVP. | **por preencher** |
| RNF-006 | Disponibilidade | Go-live no plano gratuito Render, Vercel e Neon. Não há meta de percentagem de uptime escrita. | **por preencher** |
| RNF-007 | Integridade | Packs, sessões e subscrição não podem perder saldo ou limite por um segundo pedido igual. | **por preencher** |
| RNF-008 | Usabilidade | Portal utilizável a 375 px. Marca do trainer não parte o contraste AA. Gates manuais ainda abertos. | **por preencher** |
| RNF-009 | Acessibilidade | Além do contraste AA da marca, não há meta escrita. | **por preencher** |
| RNF-010 | Compatibilidade | Web. Não há lista fechada de browsers. | **por preencher** |
| RNF-011 | Testes | Regras, autorização e contrato têm de ter teste. Comandos em `AGENTS.md`. | **por preencher** |
| RNF-012 | Operação | Sprint 7A, blueprints validados em 2026-10-25 e por aplicar. Os logs são JSON na consola, com correlation id e trace id. Os erros vão para o Sentry. Os traces e as métricas vão para o Grafana Cloud. As duas contas são gratuitas e ficam na região UE. Há seis alertas por email: erro novo, 5xx acima de 5%, job desistido, webhook Stripe falhado, leitura lenta e pool cheio. Não há sonda de uptime, para o Render e o Neon poderem adormecer. Os health checks vêm da 6C. Ver `docs/blueprints/sprint_7/00_plano_sprint_7.md`. | **por preencher** |
| RNF-013 | Alojamento | API no Render, frontend na Vercel, PostgreSQL no Neon. Sem Redis: o Gate 7B (2026-10-26, ADR-002) decidiu `Não implementar ainda`, com reavaliação no 10B. Há uma só instância, e as leituras ficam dentro dos 300 ms mesmo com +5 ms de rede (dashboard: p95 177 ms). O dashboard (18 queries), o `portal/home` e o `summary` (9 cada) continuam acima das 5 queries e não foram consolidados. Para tirar o cold start, o Leandro pondera o Render pago ($7/mês) e o Neon Launch (~$5–20/mês): `sprint_7B/01_decisao_gate_7B.md` §15. | **por preencher** |
| RNF-014 | Reembolso | A arquitectura não tem reembolso. O webhook só trata checkout, subscrição e faturas pagas ou falhadas. Não é prioridade agora. | Quando existir, é só no Stripe, sem ecrã na app. O painel desce quando o Stripe confirmar, e só a parte devolvida, mesmo que o pagamento seja de outro mês. Decisão de 2026-10-15. |
