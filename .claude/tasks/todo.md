# Sprint 4 — Fase 2: blueprints de autenticação local (2026-08-28)

## Plano

- [x] 1. Passo 0 — verificar contra o código real: registo de handlers em DI, ausência de `IAccessTokenIssuer`/`IAuthenticationEmailSender`, estado do lockout do Identity, policies de rate limiting, pacotes em falta, última migration, Docker.
- [x] 2. `fase_2/00_indice_ordem_e_dependencias.md` — âmbito, verificações com resultado, divergências Plan/código, contrato das 12 rotas, ordem de aplicação e do pipeline.
- [x] 3. `fase_2/01_dominio_e_persistencia.md` — `RefreshToken` com `CsrfTokenHash` e a configuração EF (2 blocos).
- [x] 4. `fase_2/02_application_csrf_e_sessao.md` — 19 ficheiros: transporte, validação e bootstrap do CSRF.
- [x] 5. `fase_2/03_infraestrutura_adapters.md` — 8 ficheiros: `JwtAccessTokenIssuer`, adapter de email, stores e DI.
- [x] 6. `fase_2/04_api_autenticacao_http.md` — 14 ficheiros: JWT bearer, cookies, Origin, Forwarded Headers, contratos, `AuthController`, `Program.cs`, `appsettings.json`.
- [x] 7. `fase_2/05_testes_funcionais.md` — 16 ficheiros de teste; 3 com bloco integral, 13 por tabela de cobertura.
- [x] 8. `fase_2/06_migration_add_refresh_session_csrf.md` — isolada no fim, com comandos, SQL esperado e o único delta manual permitido.
- [x] 9. `fase_2/07_gate_fase_2.md` — evidência, descobertas, desvio de formato declarado, 8 decisões abertas e quality gate por requisito.
- [x] 10. `docs/backend-files/sprint_4/README.md` atualizado com a Fase 2 e com o `05_` da Fase 1 que faltava.
- [x] 11. Memória: `MEMORY.md` itens 12 e 13, e `Sessions/2026-08-28-sprint4-fase2-blueprints.md`.
- [x] 12. Verificação: 1 caminho e 1 bloco por secção de blueprint; `backend/` intocado.

## Por fazer na materialização

- [ ] Aplicar os blueprints pela ordem do documento 00, com a migration em último lugar.
- [ ] Build Release sem warnings.
- [ ] Domain, Application, Infrastructure, Api e Architecture verdes, com contagens atualizadas (124 handlers, 72 validators).
- [ ] `dotnet format --verify-no-changes` nos projetos tocados.
- [ ] migrate → rollback → migrate contra PostgreSQL 17 real.
- [ ] Decidir as 8 questões abertas do gate, começando pela porta HTTPS e pelas versões de pacote.

## Review

O plano aprovado previa um `RequireCsrfFilter` na camada Api. Foi rejeitado
durante a produção: obrigaria a Api a aceder à base de dados, abriria uma janela
TOCTOU entre aprovar o CSRF e rodar a sessão — anulando o gate G6 — e custaria um
round-trip por refresh. A comparação passou para dentro da transação do store. O
filtro de `Origin` manteve-se na Api, porque é verificação de header pura e o
Plan §2.4 exige Origin validado antes de consultar a sessão. Consequência de
âmbito: o documento 02 cresceu de 10 para 19 secções.

Três afirmações do `Plan_sprint_4.md` foram desmentidas pelo código real e estão
registadas no documento 00 em vez de propagadas: as duas portas de autenticação
já existem na Application, o lockout do Identity já está implementado, e a
entidade é `RefreshToken` e não `RefreshSession`.

O bug com maior custo evitado é o `RoleClaimType`: sem o mapeamento para `"role"`,
ligar a autenticação faria as cinco policies da Fase 1 recusar todos os
utilizadores autenticados, com um sintoma que não aponta para a causa.

Desvio assumido: 13 dos 16 ficheiros do documento 05 estão especificados por
tabela de cobertura e não por bloco C# integral. Não cumpre a regra 1 do
blueprint; está declarado em secção própria do gate.
