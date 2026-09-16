# Estados de UI e mini design system

*Artboards 09 e 10 · Frontend 6C · 2026-09-16*

**Referência visual (09 e 10 na mesma imagem):** ![09 Estados e 10 Mini design system](<assets/screenshots of errors.png>)

## 1. Estados — artboard 09

Topo de [`assets/screenshots of errors.png`](<assets/screenshots of errors.png>).

Obrigatórios em todas as listas e formulários (Gate 6E).

### Vazio — mantém, com ajuste

- Ícone discreto, título display, uma frase, **uma** ação primária.
- Exemplo: "Sem alimentos no catálogo · Ainda não existem alimentos aprovados. Cria o
  primeiro." → "+ Novo alimento".
- **Ajuste:** remover "Importar CSV" (futuro, DEF-PROD-004) e a referência a "tabela do INSA".

### Carregamento — mantém

- Skeleton com a métrica real das linhas (48 px na tabela), shimmer 1,2 s ease-in-out;
  `aria-busy="true"`. Sem spinners isolados.

### Erro — ajustado

| No mockup | Final |
|---|---|
| Badge "ERRO 500 · 16/09/2026 09:41" | Sem código HTTP visível; data/hora opcional |
| "Não foi possível guardar o alimento" | Mantém (título humano) |
| "O serviço de catálogo não respondeu. As alterações ficaram em rascunho local e podem ser reenviadas." | "Não conseguimos guardar agora. Os dados do formulário continuam aqui; tenta novamente." — **não há rascunhos locais** |
| Correlation id copiável | Mantém: `correlation_id` do ProblemDetails (ver `../../backend/02_CONTRATO_HTTP_FRONTEND.md`) |
| "Tentar novamente" · "Reportar" | "Tentar novamente" · "Copiar id" — **não há endpoint de report** |
| `aria-live="assertive"` | Mantém |

Erros de validação (`errors[]`) vão para os campos (`form.setError`), não para este bloco.

## 2. Mini design system — artboard 10

Meio de [`assets/screenshots of errors.png`](<assets/screenshots of errors.png>).

### Tokens

Coincidem com [../03_DESIGN_SYSTEM_E_MARCA.md](../03_DESIGN_SYSTEM_E_MARCA.md) §4.

| Token | Dark (principal) | Light | Uso |
|---|---|---|---|
| `--bg` | `#05080C` | `#F7F8FA` | Fundo da app |
| `--card` | `#0A0F16` | `#FFFFFF` | Cartões, sheets |
| `--border` | `#1A2230` | `#E3E8EF` | Bordas 1 px |
| `--fg` | `#F7F8FA` | `#0B1220` | Texto primário |
| `--muted` | `#94A3B8` | `#4A5768` | Texto secundário |
| `--primary` | `#00A3E9` | `#0077B6` | Acento único |
| `--primary-fg` | `#03131C` | `#FFFFFF` | Texto sobre azul |
| `--radius` | 12 px | 12 px | Cartões; 10 px botões; 8 px controlos |

Divergências menores a resolver na 6C (script de contraste): `--border` light `#E3E8EF`
(mockup) vs `#E2E8F0` (doc 03); `--muted` light `#4A5768` (mockup) vs `#475569` (doc 03).

### Tipografia

| Estilo | Fonte | Uso |
|---|---|---|
| KPI 56 | Saira Condensed 900 itálico | KPI / hero |
| Título 36 | Saira Condensed 900 itálico | Título de página |
| Secção 22 | Saira Condensed 900 itálico | Título de cartão/sheet |
| Corpo forte 16 | Geist 600 | Ênfase |
| Corpo 14 / UI | Geist 400 | Texto e controlos |
| Mono 12 | Geist Mono, tabular | `1 284 kcal · 64,8 kg`, ids, metadados |

### Componentes base

| Componente | Especificação |
|---|---|
| Botões | 38 px, raio 10 px: Primário · Secundário · Outline · Ghost · Destrutivo · Desativado |
| Badges de estado | Cor + ícone + texto: ✓ Ativo · ▲ A expirar · ! Em atraso · • Arquivado · ○ Rascunho |
| Inputs | 40 px: repouso · foco (ring 3 px `rgba(0,163,233,.22)`) · inválido (! + mensagem) · desativado |

**Ajuste:** o badge "Rascunho" só se usa onde o backend tenha esse estado; hoje nenhuma
entidade listada no layout o tem.
