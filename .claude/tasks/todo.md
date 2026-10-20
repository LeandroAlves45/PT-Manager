# TODO: Sprint 6GB · fase Superuser (6GB-SU)

Plano aprovado a 2026-10-19 em `~/.claude/plans/compressed-wishing-charm.md`. Decisões U39–U52.
Branch `feat/6gb-superuser`, base `df88341` (main). Instruções completas em `.claude/SPEC.md`.

## 0. Arranque
- [x] Prints `admin_*`, `docs_about_app`, `sprint_6G/00`, gotchas e lições lidos
- [x] Decisões U39–U52 com o Leandro (âmbito, faturação líquida, Stripe, página Treinadores)
- [x] Branch, `todo.md`, `SPEC.md`, `sprint_6GB/00_plano_6GB_superuser.md`

## 1. Backend (SU-1)
- [x] Momento opcional (domínio, validadores, contratos, check, consumidores)
- [x] `PlatformSubscriptionPayment` + gateway Stripe do líquido + webhook `invoice.paid`
- [x] Visão geral RN-018 (ativos, suspensos, isentos, recebido este mês)
- [x] Isenção: `BillingExemptSince`, isentar/tirar, cancelar Stripe, isenção vence o snapshot
- [x] Lookup por email + lista de isentos
- [x] `TrainerInvitation`: criar, listar, anular, status, register, email
- [x] Migration gerada, `schema.d.ts` regenerado
- [x] Testes de caminho real (unit, integração, funcionais) e suite completa verde

## 2. Frontend (SU-2)
- [x] Catálogo em páginas (`new`, `:id`), vídeo na criação, títulos/botões, «ex.: Barra», Momento opcional
- [x] `MuscleGroupPicker` desliza com a roda (provado no browser)
- [x] Aviso de alterações por guardar
- [x] Moderação: Tabs no tipo, lista «Estado»
- [x] Visão geral: secções, plurais, destaque nos suspensos
- [x] Página Treinadores (procura, ficha, isentos, convites, convidar)
- [x] Página `/convite-treinador`
- [x] Rotas lazy do admin
- [x] Testes Vitest de caminho real

## 3. Verificação e fecho (SU-3)
- [x] Mutações (backend e frontend), todas mortas ou justificadas
- [x] Browser 375 e 1440 px, dois temas, fluxo isentar/convidar/aceitar
- [x] `security-reviewer` + `code-review-leandro` (sonnet), achados provados
- [x] `design-is` aos ecrãs do admin
- [x] Migration aplicada à BD de dev
- [x] Docs (`obsidian-ptmanager`): `docs_about_app`, `00_plano_sprint_6G`, relatório 01, memória, gotchas
- [x] `SPEC.md` marcado Finalizado e apagado

## Review

FINALIZADO 2026-10-20. Backend 3223 + 1 skip, frontend 791/791, mutações 13/13, security sem achados,
code review com 5 defeitos corrigidos, `design-is` 20/30 REFINE, browser com MSW a 1440/375 nos dois temas.
Defeitos D1–D9 no relatório `sprint_6GB/01_relatorio_fecho_6GB_superuser.md` §3.
Commits `f5f905d`..`f4394bf` na `main`; CI #46 verde (o #45 falhou no contrato OpenAPI, corrigido).
Por fazer pelo Leandro: os gates `QG6GB-SU-GATE-001`, `-STRIPE-001` e `-VIDEO-001`
(o browser real com login de superuser não foi feito: a palavra-passe do seed está nos User Secrets).
