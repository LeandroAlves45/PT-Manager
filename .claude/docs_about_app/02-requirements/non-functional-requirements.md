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
| RNF-012 | Operação | Logs estruturados, Sentry e OpenTelemetry estão Planeados no Sprint 7A. Health checks já foram antecipados na 6C. | **por preencher** |
| RNF-013 | Alojamento | API no Render, frontend na Vercel, PostgreSQL no Neon. Redis só se o Gate 7B o mandar. | **por preencher** |
| RNF-014 | Reembolso | A arquitectura não tem reembolso. O webhook só trata checkout, subscrição e faturas pagas ou falhadas. Não é prioridade agora. | Quando existir, é só no Stripe, sem ecrã na app. O painel desce quando o Stripe confirmar, e só a parte devolvida, mesmo que o pagamento seja de outro mês. Decisão de 2026-10-15. |
