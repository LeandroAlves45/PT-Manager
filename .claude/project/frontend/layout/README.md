# Layout do Sprint 6 — referência visual e especificação por ecrã

*Criado em 2026-09-16. Fonte visual aprovada pelo utilizador: Claude Design.*

## Fonte visual

| Fonte | Link / caminho | Papel |
|---|---|---|
| **Claude Design** — "PT Manager Layout System" (13 artboards, PT-PT) | Screenshots em [`assets/`](assets/) (ver tabela abaixo) | **Base canónica** do layout (dark principal; 01, 04 e 07 também em light) |
| v0 — app-shell (Next.js, página única) | `C:\Users\Leandro Alves\Desktop\Projeto pt_manager\pt-manager-app-shell` (fora do repo) | Só comparação. Único elemento adoptado: **dropdown de perfil** na topbar |

> **Regra para agentes:** a referência visual são os **PNG em `assets/`**, lidos com a
> ferramenta de leitura de imagens. **Não** abrir o projeto no Claude Design (exige browser
> autenticado e iframe) **nem** usar PDF (consome muitos mais tokens). Ler só a imagem do
> ecrã em causa.

Este layout **não** é contrato. Onde o mockup diverge do backend real, prevalece o código
(`backend/`) e as decisões registadas em [01_RELATORIO_ANALISE.md](01_RELATORIO_ANALISE.md).

## Ordem de leitura

| # | Documento | Artboards | Fase |
|---|---|---|---|
| 1 | [01_RELATORIO_ANALISE.md](01_RELATORIO_ANALISE.md) | todos | decisões para 6A–6G |
| 2 | [02_APPSHELL_NAVEGACAO.md](02_APPSHELL_NAVEGACAO.md) | 01, 01-light, 02, 08 | 6C |
| 3 | [03_ECRAS_ADMIN.md](03_ECRAS_ADMIN.md) | 03 | 6B (backend) · 6D |
| 4 | [04_ECRAS_TRAINER.md](04_ECRAS_TRAINER.md) | 04, 04-light, 05, 06 | 6A/6B (backend) · 6E |
| 5 | [05_ECRAS_CLIENTE.md](05_ECRAS_CLIENTE.md) | 02 (portal), 07, 07-light | 6A/6B (backend) · 6F |
| 6 | [06_ESTADOS_E_TOKENS.md](06_ESTADOS_E_TOKENS.md) | 09, 10 | 6C |

Documentos relacionados: [../03_DESIGN_SYSTEM_E_MARCA.md](../03_DESIGN_SYSTEM_E_MARCA.md),
[../04_BENCHMARK_E_FUNCIONALIDADES.md](../04_BENCHMARK_E_FUNCIONALIDADES.md),
[../../02_SPRINTS_ROADMAP.md](../../02_SPRINTS_ROADMAP.md) §Sprint 6,
[../../sprints/sprint-6/README.md](../../sprints/sprint-6/README.md).

## Screenshots (`assets/`)

Cada PNG agrupa vários artboards. Os nomes têm espaços: em Markdown usam-se entre `< >`.

| Ficheiro | Artboards (de cima para baixo) | Usado em |
|---|---|---|
| [`assets/appshell desktop.png`](<assets/appshell desktop.png>) | 01 AppShell desktop 1440 · dark (expandida e recolhida) | 02 |
| [`assets/appshell mobile and admin.png`](<assets/appshell mobile and admin.png>) | 02 AppShell mobile 390 (Sheet + portal) · 03 Admin catálogo global de alimentos | 02 · 03 · 05 |
| [`assets/trainer dash.png`](<assets/trainer dash.png>) | Rodapé do Sheet do 03 · 04 Trainer dashboard (bento) · dark · 05 Trainer detalhe do cliente | 04 |
| [`assets/client and trainer screenshots.png`](<assets/client and trainer screenshots.png>) | Fim do 05 · 06 Adicionar alimento / grupos musculares · 07 Cliente treino de hoje · dark | 04 · 05 |
| [`assets/screenshots of errors.png`](<assets/screenshots of errors.png>) | 09 Estados (vazio, carregamento, erro) · 10 Mini design system · início do 01 light | 06 |
| [`assets/light mode.png`](<assets/light mode.png>) | 01 light · 04 light · 07 light (só o topo) | 02 · 04 · 05 |

Sem screenshot: **08 Command palette ⌘K** (especificação textual em
[02_APPSHELL_NAVEGACAO.md](02_APPSHELL_NAVEGACAO.md) §6) e o 07 light completo.

As imagens mostram o mockup **original**; os ajustes obrigatórios de cada documento têm
precedência sobre a imagem.

## Como usar nas fases

1. Antes de gerar o blueprint de uma fase de frontend, ler o documento do ecrã e a secção
   "Ajustes obrigatórios".
2. Cada bloco visual indica o endpoint de origem. Endpoints marcados **6A/6B** ainda não
   existem; os caminhos finais são fixados nesses blueprints.
3. Nada marcado **Excluído** ou **Futuro** é desenhado como funcional.
