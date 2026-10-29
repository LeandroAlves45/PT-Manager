# Catálogo global inicial (exercícios e alimentos)

Dois ficheiros CSV prontos a importar no painel do superuser:

| Ficheiro | Linhas | Origem | Licença |
|---|---|---|---|
| `exercicios.csv` | 138 | [free-exercise-db](https://github.com/yuhonas/free-exercise-db) | Unlicense (domínio público) |
| `alimentos.csv` | 156 | [USDA FoodData Central](https://fdc.nal.usda.gov/), SR Legacy (abril 2018) | CC0 1.0 (domínio público) |

**Revê antes de importar.** Os ficheiros abrem diretamente no Excel em português.

## Formato

- Separador `;`, decimais com vírgula (`25,5`; ponto também é aceite), UTF-8.
- No Excel, grava sempre como **"CSV UTF-8 (delimitado por vírgulas)"**. Os outros formatos CSV
  estragam os acentos e a importação recusa o ficheiro.
- A primeira linha é o cabeçalho. A ordem das colunas é livre, mas os nomes têm de ser estes.

### Alimentos (`alimentos.csv`)

| Coluna | Obrigatória | Regra |
|---|---|---|
| `nome` | sim | até 255 caracteres; não pode começar por `=`, `+`, `-` ou `@` |
| `descricao` | não | texto livre |
| `proteina` | sim | gramas por 100 g, de 0 a 100 |
| `hidratos` | sim | gramas por 100 g, de 0 a 100 |
| `gordura` | sim | gramas por 100 g, de 0 a 100 |
| `fibra` | não | gramas por 100 g, de 0 a 100 |
| `porcao_g` | não | porção habitual em gramas, maior que 0 e até 1000 |

Proteína + hidratos + gordura não podem passar de 100 g. As kcal são calculadas pela app
(4 × proteína + 4 × hidratos + 9 × gordura).

**Como os valores foram obtidos:**
- Proteína (nutriente 1003), gordura (1004) e fibra (1079) da USDA, arredondadas a 0,1 g.
- **Hidratos = hidratos por diferença (1005) − fibra.** A USDA inclui a fibra nos hidratos.
  Os rótulos da UE não a incluem, e a app guarda a fibra à parte.
- A descrição de cada linha tem o **FDC ID** de origem. Para conferires um alimento, abre
  `https://fdc.nal.usda.gov/food-details/<FDC ID>/nutrients`.
- Alguns alimentos portugueses não existem na USDA e usam o equivalente mais próximo, assinalado
  na descrição: requeijão (ricota), queijo flamengo (gouda), bacalhau (bacalhau fresco do
  Atlântico) e leite meio-gordo (leite com 2% de gordura).
- O chouriço ficou de fora: a USDA só tem chouriço mexicano cru, com valores muito diferentes.
- A porção (`porcao_g`) é uma sugestão. Ajusta-a ao que faz sentido para os teus clientes.

Atribuição pedida pela USDA: *U.S. Department of Agriculture, Agricultural Research Service.
FoodData Central, 2019. fdc.nal.usda.gov.*

### Exercícios (`exercicios.csv`)

| Coluna | Obrigatória | Regra |
|---|---|---|
| `nome` | sim | até 255 caracteres; não pode começar por `=`, `+`, `-` ou `@` |
| `descricao` | não | texto livre |
| `grupos_musculares` | não | códigos separados por vírgula (tabela abaixo) |
| `equipamento` | não | texto livre, até 255 caracteres |
| `dificuldade` | não | `Iniciante`, `Intermédio` ou `Avançado` |

Não há coluna de vídeo. O vídeo de um exercício global envia-se depois, no editor do catálogo.

| Código | Grupo | Código | Grupo |
|---|---|---|---|
| `chest` | Peito | `triceps` | Tríceps |
| `back` | Costas | `forearms` | Antebraços |
| `lats` | Dorsais | `core` | Core |
| `traps` | Trapézio | `glutes` | Glúteos |
| `lower_back` | Lombar | `quadriceps` | Quadríceps |
| `shoulders` | Ombros | `hamstrings` | Isquiotibiais |
| `biceps` | Bíceps | `calves` | Gémeos |

**Como os valores foram obtidos:**
- Nome e descrição foram escritos em português para cada exercício.
- Os grupos musculares vêm dos músculos primários e secundários da fonte. Os adutores e o
  pescoço não têm grupo na app e ficaram de fora.
- O equipamento e a dificuldade vêm da fonte: `beginner` passa a Iniciante, `intermediate` a
  Intermédio e `expert` a Avançado.
- Corrigi à mão o equipamento de 12 exercícios em que a fonte não o indicava. Também corrigi os
  grupos de 5 exercícios em que a fonte era pouco plausível: Pallof press, elevações laterais na
  polia, escaladores, prancha lateral e adução na máquina.

## Como importar

1. Entra como superuser:
   - Dev: `https://localhost:5173/entrar`
   - Produção: `https://app.<dominio>/entrar`
2. Vai a **Catálogo → Exercícios → Importar CSV** e escolhe `exercicios.csv`.
3. **Verificar**:
   - Mostra os erros por linha sem gravar nada.
   - Se estiver tudo certo, mostra quantos vão ser criados e quais já existem.
4. **Importar** grava tudo numa única operação. Cada item fica registado na auditoria administrativa.
5. Repete os passos 2 a 4 para **Alimentos** com `alimentos.csv`.

**Reimportar é seguro.**
- Os nomes que já existem no catálogo são saltados, incluindo os arquivados.
- A comparação ignora maiúsculas, acentos e espaços a mais.
- Para corrigir um item já importado, usa o editor do catálogo.

**Em produção:** antes de importar, cria um branch na Neon como ponto de recuperação. Os ficheiros
são os mesmos e não há SQL nem migration.

**Limites:** 1 MB e 1000 linhas por ficheiro. Divide ficheiros maiores.
