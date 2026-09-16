# Ecrãs do superuser (admin)

*Artboard 03 · Backend 6B (fila de moderação) · Frontend 6D · 2026-09-16*

## 1. Catálogo global de alimentos

**Referência visual:** ![03 Admin catálogo de alimentos](<assets/appshell mobile and admin.png>)
— metade inferior (artboard 03). O rodapé do Sheet (Cancelar · Guardar alterações) está
no topo de [`assets/trainer dash.png`](<assets/trainer dash.png>).

### Mantém-se

- Densidade utilitária: linhas 48 px, célula padding 10/14, cabeçalho com fundo `#0C121A`.
- Pesquisa + filtro em Tabs **Ativos · Arquivados · Todos**.
- Sheet de edição 440 px à direita, slide 200 ms, rodapé fixo (Cancelar · Guardar alterações).
- Ações por linha sempre visíveis (teclado e toque).
- Paginação por página.
- Contagem no título ("1 284 alimentos") a partir de `total_count`.

### Ajustes obrigatórios

| No mockup | Ajuste |
|---|---|
| Coluna e campo **Categoria** | Remover (excluído) |
| Linha secundária "fd_0192 · INSA 2019" / "submetido por PT" | Remover a fonte; o id não é mostrado ao utilizador |
| **Kcal** editável | Só leitura, recalculada ao editar P/HC/G (`P×4 + HC×4 + G×9`) |
| **Porção padrão** | Mantém-se, mas só depois da 6A (`DefaultServingGrams`, opcional) |
| Switch "Visível no catálogo" | Remover do formulário; ação **Arquivar/Reativar** na linha e no Sheet, com ConfirmDialog ("deixa de estar disponível para todos os trainers") |
| "Notas de moderação" | Remover (excluído) |
| "Última moderação 14/09/2026" no subtítulo | Remover (não há esse dado) |
| "25 por página" | Pedido explícito `page_size=25` (omissão do backend é 50, máximo 100) |

### Colunas finais

Alimento · Kcal/100 g · P (g) · HC (g) · G (g) · Fibra (g) · Estado · Ações.

### Endpoints

| Ação | Endpoint |
|---|---|
| Listar e pesquisar | `GET /global-foods?activity=&search=&page_number=&page_size=` |
| Detalhe | `GET /global-foods/{foodId}` |
| Criar / editar | `POST /global-foods` · `PATCH /global-foods/{foodId}` |
| Arquivar / reativar | `POST /global-foods/{foodId}/archive` · `/reactivate` |
| Apagar | `DELETE /global-foods/{foodId}` (ConfirmDialog; erros de negócio mapeados a partir do ProblemDetails — códigos confirmados no blueprint 6D) |

Exercícios e suplementos globais seguem o mesmo molde (`/global-exercises`,
`/global-supplements`). Exercícios globais incluem o estado do vídeo (5D) e a regra
`409 global_exercise_has_video` ao apagar.

## 2. Fila de moderação (sem artboard — a desenhar na 6D)

Hoje o superuser só pode bloquear por id
(`POST /admin/content-moderation/{foods|exercises}/{id}/block|unblock`). A 6B cria a
listagem paginada de conteúdo privado.

Especificação de UI (mesma densidade do catálogo):

- Tabs **Alimentos · Exercícios**; filtro por estado de enforcement (permitido · bloqueado).
- Colunas: nome, trainer dono, estado, motivo, data da decisão, ações.
- Bloquear abre diálogo com motivo (`BlockContentRequest.ReasonCode`) entre os valores fixos do backend
  (`malicious_content`, `dangerous_information`, `deliberately_false_information`,
  `prohibited_content`), com labels PT-PT.
- Desbloquear com ConfirmDialog.
- Cada escrita gera auditoria administrativa (Gate 6D verifica no backend).

Não confundir com DEF-TRUST-004 (denúncias e evidência), que continua no 10A.

## 3. Visão geral do admin (sem artboard)

Cartões com `total_count` de cada catálogo e da fila de moderação. Sem gráficos nem
métricas que o backend não devolva.
