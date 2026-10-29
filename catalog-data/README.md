# Catálogo global inicial (exercícios e alimentos)

Dois ficheiros CSV prontos a importar no painel do superuser:

| Ficheiro | Linhas | Origem | Licença |
|---|---|---|---|
| `exercicios.csv` | 138 | [free-exercise-db](https://github.com/yuhonas/free-exercise-db) | Unlicense (domínio público) |
| `alimentos.csv` | 181 | [USDA FoodData Central](https://fdc.nal.usda.gov/), SR Legacy (abril 2018) | CC0 1.0 (domínio público) |

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
| `descricao` | não | texto livre (o ficheiro fornecido não a usa) |
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
- O ficheiro não tem coluna `descricao`, para nada aparecer na app além do nome. A origem de cada
  alimento (FDC ID, descrição da USDA e notas) está na tabela [Origem de cada alimento](#origem-de-cada-alimento).
- Cozinhados: grelhados e assados sem gordura adicionada, salvo os ovos mexidos e estrelados
  (receita da USDA). As carnes são a parte magra quando a nota o diz.
- Alguns alimentos portugueses não existem na USDA e usam o equivalente mais próximo, assinalado
  na tabela: requeijão (ricota), queijo flamengo (gouda), bacalhau (bacalhau fresco do
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

## Origem de cada alimento

Valores por 100 g da USDA FoodData Central (SR Legacy). Cada FDC ID abre a página do alimento.

| Alimento | FDC ID | Descrição na USDA | Nota |
|---|---|---|---|
| Peito de frango cru | [171077](https://fdc.nal.usda.gov/food-details/171077/nutrients) | Chicken, broiler or fryers, breast, skinless, boneless, meat only, raw | Sem pele. |
| Peito de frango assado | [171477](https://fdc.nal.usda.gov/food-details/171477/nutrients) | Chicken, broilers or fryers, breast, meat only, cooked, roasted | Sem pele. |
| Peito de frango grelhado | [171534](https://fdc.nal.usda.gov/food-details/171534/nutrients) | Chicken, broiler or fryers, breast, skinless, boneless, meat only, cooked, grilled | Sem pele. |
| Coxa de frango crua | [173627](https://fdc.nal.usda.gov/food-details/173627/nutrients) | Chicken, broilers or fryers, dark meat, thigh, meat only, raw | Sem pele. |
| Coxa de frango assada | [172388](https://fdc.nal.usda.gov/food-details/172388/nutrients) | Chicken, broilers or fryers, thigh, meat only, cooked, roasted | Sem pele. |
| Peito de peru cru | [171098](https://fdc.nal.usda.gov/food-details/171098/nutrients) | Turkey, whole, breast, meat only, raw | Sem pele. |
| Peito de peru assado | [171496](https://fdc.nal.usda.gov/food-details/171496/nutrients) | Turkey, whole, breast, meat only, cooked, roasted | Sem pele. |
| Peru picado cozinhado | [171506](https://fdc.nal.usda.gov/food-details/171506/nutrients) | Turkey, Ground, cooked |  |
| Carne de vaca picada 5% gordura crua | [171790](https://fdc.nal.usda.gov/food-details/171790/nutrients) | Beef, ground, 95% lean meat / 5% fat, raw |  |
| Carne de vaca picada 10% gordura crua | [174030](https://fdc.nal.usda.gov/food-details/174030/nutrients) | Beef, ground, 90% lean meat / 10% fat, raw |  |
| Carne de vaca picada 20% gordura crua | [174036](https://fdc.nal.usda.gov/food-details/174036/nutrients) | Beef, ground, 80% lean meat / 20% fat, raw |  |
| Hambúrguer de vaca 5% gordura grelhado | [171791](https://fdc.nal.usda.gov/food-details/171791/nutrients) | Beef, ground, 95% lean meat / 5% fat, patty, cooked, broiled |  |
| Hambúrguer de vaca 10% gordura grelhado | [174031](https://fdc.nal.usda.gov/food-details/174031/nutrients) | Beef, ground, 90% lean meat / 10% fat, patty, cooked, broiled |  |
| Bife de vaca (alcatra) cru | [174055](https://fdc.nal.usda.gov/food-details/174055/nutrients) | Beef, top sirloin, steak, separable lean only, trimmed to 1/8" fat, all grades, raw | Parte magra. |
| Bife de vaca (alcatra) grelhado | [168634](https://fdc.nal.usda.gov/food-details/168634/nutrients) | Beef, top sirloin, steak, separable lean only, trimmed to 0" fat, all grades, cooked, broiled | Parte magra. |
| Bife do lombo de vaca grelhado | [171811](https://fdc.nal.usda.gov/food-details/171811/nutrients) | Beef, tenderloin, steak, separable lean only, trimmed to 1/8" fat, all grades, cooked, broiled | Parte magra. |
| Lombo de porco cru | [168249](https://fdc.nal.usda.gov/food-details/168249/nutrients) | Pork, fresh, loin, tenderloin, separable lean only, raw | Parte magra. |
| Lombo de porco assado | [168250](https://fdc.nal.usda.gov/food-details/168250/nutrients) | Pork, fresh, loin, tenderloin, separable lean only, cooked, roasted | Parte magra. |
| Febras de porco cruas | [167829](https://fdc.nal.usda.gov/food-details/167829/nutrients) | Pork, fresh, loin, center loin (chops), bone-in, separable lean only, raw | Parte magra. |
| Febras de porco grelhadas | [168240](https://fdc.nal.usda.gov/food-details/168240/nutrients) | Pork, fresh, loin, center loin (chops), bone-in, separable lean only, cooked, broiled | Parte magra. |
| Perna de borrego assada | [172516](https://fdc.nal.usda.gov/food-details/172516/nutrients) | Lamb, New Zealand, imported, frozen, leg, whole (shank and sirloin), separable lean only, cooked, roasted | Parte magra. |
| Fígado de vaca cru | [169451](https://fdc.nal.usda.gov/food-details/169451/nutrients) | Beef, variety meats and by-products, liver, raw |  |
| Coelho cru | [172521](https://fdc.nal.usda.gov/food-details/172521/nutrients) | Game meat, rabbit, domesticated, composite of cuts, raw |  |
| Coelho assado | [172522](https://fdc.nal.usda.gov/food-details/172522/nutrients) | Game meat, rabbit, domesticated, composite of cuts, cooked, roasted |  |
| Fiambre de porco | [173864](https://fdc.nal.usda.gov/food-details/173864/nutrients) | Ham, sliced, regular (approximately 11% fat) |  |
| Fiambre de peru | [174572](https://fdc.nal.usda.gov/food-details/174572/nutrients) | Turkey breast, low salt, prepackaged or deli, luncheon meat |  |
| Bacon cru | [168277](https://fdc.nal.usda.gov/food-details/168277/nutrients) | Pork, cured, bacon, unprepared |  |
| Salmão cru | [175167](https://fdc.nal.usda.gov/food-details/175167/nutrients) | Fish, salmon, Atlantic, farmed, raw | Aquacultura. |
| Salmão grelhado | [175168](https://fdc.nal.usda.gov/food-details/175168/nutrients) | Fish, salmon, Atlantic, farmed, cooked, dry heat | Aquacultura. |
| Bacalhau cru | [171955](https://fdc.nal.usda.gov/food-details/171955/nutrients) | Fish, cod, Atlantic, raw | Valores de bacalhau fresco; o bacalhau demolhado é semelhante. |
| Bacalhau cozinhado | [171956](https://fdc.nal.usda.gov/food-details/171956/nutrients) | Fish, cod, Atlantic, cooked, dry heat | Valores de bacalhau fresco. |
| Atum em lata ao natural | [173709](https://fdc.nal.usda.gov/food-details/173709/nutrients) | Fish, tuna, light, canned in water, drained solids (Includes foods for USDA's Food Distribution Program) | Escorrido. |
| Atum em lata em óleo | [173708](https://fdc.nal.usda.gov/food-details/173708/nutrients) | Fish, tuna, light, canned in oil, drained solids | Escorrido. |
| Atum fresco grelhado | [172006](https://fdc.nal.usda.gov/food-details/172006/nutrients) | Fish, tuna, yellowfin, fresh, cooked, dry heat | Atum albacora. |
| Sardinhas em lata em óleo | [175139](https://fdc.nal.usda.gov/food-details/175139/nutrients) | Fish, sardine, Atlantic, canned in oil, drained solids with bone | Escorridas, com espinha. |
| Cavala crua | [175119](https://fdc.nal.usda.gov/food-details/175119/nutrients) | Fish, mackerel, Atlantic, raw |  |
| Cavala grelhada | [175120](https://fdc.nal.usda.gov/food-details/175120/nutrients) | Fish, mackerel, Atlantic, cooked, dry heat |  |
| Robalo cru | [175142](https://fdc.nal.usda.gov/food-details/175142/nutrients) | Fish, sea bass, mixed species, raw |  |
| Robalo grelhado | [173694](https://fdc.nal.usda.gov/food-details/173694/nutrients) | Fish, sea bass, mixed species, cooked, dry heat |  |
| Tilápia crua | [175176](https://fdc.nal.usda.gov/food-details/175176/nutrients) | Fish, tilapia, raw |  |
| Tilápia grelhada | [175177](https://fdc.nal.usda.gov/food-details/175177/nutrients) | Fish, tilapia, cooked, dry heat |  |
| Truta crua | [173717](https://fdc.nal.usda.gov/food-details/173717/nutrients) | Fish, trout, rainbow, farmed, raw | Aquacultura. |
| Truta grelhada | [173718](https://fdc.nal.usda.gov/food-details/173718/nutrients) | Fish, trout, rainbow, farmed, cooked, dry heat | Aquacultura. |
| Espadarte cru | [173703](https://fdc.nal.usda.gov/food-details/173703/nutrients) | Fish, swordfish, raw |  |
| Espadarte grelhado | [173704](https://fdc.nal.usda.gov/food-details/173704/nutrients) | Fish, swordfish, cooked, dry heat |  |
| Polvo cru | [174218](https://fdc.nal.usda.gov/food-details/174218/nutrients) | Mollusks, octopus, common, raw |  |
| Polvo cozido | [174249](https://fdc.nal.usda.gov/food-details/174249/nutrients) | Mollusks, octopus, common, cooked, moist heat |  |
| Lulas cruas | [174223](https://fdc.nal.usda.gov/food-details/174223/nutrients) | Mollusks, squid, mixed species, raw |  |
| Camarão cru | [175179](https://fdc.nal.usda.gov/food-details/175179/nutrients) | Crustaceans, shrimp, raw |  |
| Camarão cozido | [175180](https://fdc.nal.usda.gov/food-details/175180/nutrients) | Crustaceans, shrimp, cooked |  |
| Mexilhão cru | [174216](https://fdc.nal.usda.gov/food-details/174216/nutrients) | Mollusks, mussel, blue, raw |  |
| Ovo inteiro cru | [171287](https://fdc.nal.usda.gov/food-details/171287/nutrients) | Egg, whole, raw, fresh | Um ovo médio tem cerca de 50 g. |
| Ovo cozido | [173424](https://fdc.nal.usda.gov/food-details/173424/nutrients) | Egg, whole, cooked, hard-boiled |  |
| Ovos mexidos | [172187](https://fdc.nal.usda.gov/food-details/172187/nutrients) | Egg, whole, cooked, scrambled | Receita da USDA; os valores dependem da gordura usada. |
| Ovo estrelado | [173423](https://fdc.nal.usda.gov/food-details/173423/nutrients) | Egg, whole, cooked, fried | Receita da USDA; os valores dependem da gordura usada. |
| Clara de ovo crua | [172183](https://fdc.nal.usda.gov/food-details/172183/nutrients) | Egg, white, raw, fresh |  |
| Gema de ovo crua | [172184](https://fdc.nal.usda.gov/food-details/172184/nutrients) | Egg, yolk, raw, fresh |  |
| Leite magro | [171269](https://fdc.nal.usda.gov/food-details/171269/nutrients) | Milk, nonfat, fluid, with added vitamin A and vitamin D (fat free or skim) |  |
| Leite meio-gordo | [171267](https://fdc.nal.usda.gov/food-details/171267/nutrients) | Milk, reduced fat, fluid, 2% milkfat, with added vitamin A and vitamin D | Valores de leite com 2% de gordura. |
| Leite gordo | [171265](https://fdc.nal.usda.gov/food-details/171265/nutrients) | Milk, whole, 3.25% milkfat, with added vitamin D |  |
| Iogurte natural | [171284](https://fdc.nal.usda.gov/food-details/171284/nutrients) | Yogurt, plain, whole milk |  |
| Iogurte natural magro | [170887](https://fdc.nal.usda.gov/food-details/170887/nutrients) | Yogurt, plain, skim milk |  |
| Iogurte grego natural | [171304](https://fdc.nal.usda.gov/food-details/171304/nutrients) | Yogurt, Greek, plain, whole milk |  |
| Iogurte grego magro | [170894](https://fdc.nal.usda.gov/food-details/170894/nutrients) | Yogurt, Greek, plain, nonfat (Includes foods for USDA's Food Distribution Program) |  |
| Queijo cottage | [172182](https://fdc.nal.usda.gov/food-details/172182/nutrients) | Cheese, cottage, lowfat, 2% milkfat |  |
| Requeijão | [170851](https://fdc.nal.usda.gov/food-details/170851/nutrients) | Cheese, ricotta, whole milk | Valores de ricota de leite gordo. |
| Queijo flamengo | [171241](https://fdc.nal.usda.gov/food-details/171241/nutrients) | Cheese, gouda | Valores de queijo gouda. |
| Queijo mozzarella | [170847](https://fdc.nal.usda.gov/food-details/170847/nutrients) | Cheese, mozzarella, part skim milk |  |
| Queijo parmesão | [170848](https://fdc.nal.usda.gov/food-details/170848/nutrients) | Cheese, parmesan, hard |  |
| Queijo cheddar | [173414](https://fdc.nal.usda.gov/food-details/173414/nutrients) | Cheese, cheddar (Includes foods for USDA's Food Distribution Program) |  |
| Queijo feta | [173420](https://fdc.nal.usda.gov/food-details/173420/nutrients) | Cheese, feta |  |
| Manteiga | [173410](https://fdc.nal.usda.gov/food-details/173410/nutrients) | Butter, salted | Com sal. |
| Natas | [170859](https://fdc.nal.usda.gov/food-details/170859/nutrients) | Cream, fluid, heavy whipping | Natas para bater. |
| Bebida de soja | [172446](https://fdc.nal.usda.gov/food-details/172446/nutrients) | Soymilk, original and vanilla, unfortified | Sem açúcar adicionado. |
| Bebida de amêndoa | [174832](https://fdc.nal.usda.gov/food-details/174832/nutrients) | Beverages, almond milk, unsweetened, shelf stable | Sem açúcar. |
| Arroz agulha cru | [168877](https://fdc.nal.usda.gov/food-details/168877/nutrients) | Rice, white, long-grain, regular, raw, enriched |  |
| Arroz agulha cozido | [168878](https://fdc.nal.usda.gov/food-details/168878/nutrients) | Rice, white, long-grain, regular, enriched, cooked |  |
| Arroz integral cru | [169703](https://fdc.nal.usda.gov/food-details/169703/nutrients) | Rice, brown, long-grain, raw (Includes foods for USDA's Food Distribution Program) |  |
| Arroz integral cozido | [169704](https://fdc.nal.usda.gov/food-details/169704/nutrients) | Rice, brown, long-grain, cooked (Includes foods for USDA's Food Distribution Program) |  |
| Massa crua | [169736](https://fdc.nal.usda.gov/food-details/169736/nutrients) | Pasta, dry, enriched |  |
| Massa cozida | [169737](https://fdc.nal.usda.gov/food-details/169737/nutrients) | Pasta, cooked, enriched, without added salt |  |
| Massa integral crua | [169738](https://fdc.nal.usda.gov/food-details/169738/nutrients) | Pasta, whole-wheat, dry (Includes foods for USDA's Food Distribution Program) |  |
| Massa integral cozida | [168910](https://fdc.nal.usda.gov/food-details/168910/nutrients) | Pasta, whole-wheat, cooked (Includes foods for USDA's Food Distribution Program) |  |
| Flocos de aveia | [173904](https://fdc.nal.usda.gov/food-details/173904/nutrients) | Cereals, oats, regular and quick, not fortified, dry |  |
| Granola | [171646](https://fdc.nal.usda.gov/food-details/171646/nutrients) | Cereals ready-to-eat, granola, homemade |  |
| Pão branco | [174924](https://fdc.nal.usda.gov/food-details/174924/nutrients) | Bread, white, commercially prepared (includes soft bread crumbs) |  |
| Pão integral | [172688](https://fdc.nal.usda.gov/food-details/172688/nutrients) | Bread, whole-wheat, commercially prepared |  |
| Pão de centeio | [172684](https://fdc.nal.usda.gov/food-details/172684/nutrients) | Bread, rye |  |
| Pão pita | [174915](https://fdc.nal.usda.gov/food-details/174915/nutrients) | Bread, pita, white, enriched |  |
| Tortilha de trigo | [175037](https://fdc.nal.usda.gov/food-details/175037/nutrients) | Tortillas, ready-to-bake or -fry, flour, refrigerated |  |
| Bolachas de arroz | [170250](https://fdc.nal.usda.gov/food-details/170250/nutrients) | Snacks, rice cakes, brown rice, plain, unsalted | Arroz integral. |
| Farinha de trigo | [168894](https://fdc.nal.usda.gov/food-details/168894/nutrients) | Wheat flour, white, all-purpose, enriched, bleached |  |
| Quinoa crua | [168874](https://fdc.nal.usda.gov/food-details/168874/nutrients) | Quinoa, uncooked |  |
| Quinoa cozida | [168917](https://fdc.nal.usda.gov/food-details/168917/nutrients) | Quinoa, cooked |  |
| Cuscuz cozido | [169700](https://fdc.nal.usda.gov/food-details/169700/nutrients) | Couscous, cooked |  |
| Batata crua | [170026](https://fdc.nal.usda.gov/food-details/170026/nutrients) | Potatoes, flesh and skin, raw | Com pele. |
| Batata cozida | [170440](https://fdc.nal.usda.gov/food-details/170440/nutrients) | Potatoes, boiled, cooked without skin, flesh, without salt | Sem pele. |
| Batata-doce crua | [168482](https://fdc.nal.usda.gov/food-details/168482/nutrients) | Sweet potato, raw, unprepared (Includes foods for USDA's Food Distribution Program) |  |
| Batata-doce assada | [168483](https://fdc.nal.usda.gov/food-details/168483/nutrients) | Sweet potato, cooked, baked in skin, flesh, without salt | Com pele. |
| Milho doce em lata | [169214](https://fdc.nal.usda.gov/food-details/169214/nutrients) | Corn, sweet, yellow, canned, whole kernel, drained solids | Escorrido. |
| Pipocas | [167959](https://fdc.nal.usda.gov/food-details/167959/nutrients) | Snacks, popcorn, air-popped | Feitas a ar quente, sem gordura. |
| Feijão-frade cozido | [173759](https://fdc.nal.usda.gov/food-details/173759/nutrients) | Cowpeas, common (blackeyes, crowder, southern), mature seeds, cooked, boiled, without salt |  |
| Feijão preto cozido | [173735](https://fdc.nal.usda.gov/food-details/173735/nutrients) | Beans, black, mature seeds, cooked, boiled, without salt |  |
| Feijão vermelho cozido | [175194](https://fdc.nal.usda.gov/food-details/175194/nutrients) | Beans, kidney, red, mature seeds, cooked, boiled, without salt |  |
| Feijão branco cozido | [175203](https://fdc.nal.usda.gov/food-details/175203/nutrients) | Beans, white, mature seeds, cooked, boiled, without salt |  |
| Grão-de-bico cozido | [173757](https://fdc.nal.usda.gov/food-details/173757/nutrients) | Chickpeas (garbanzo beans, bengal gram), mature seeds, cooked, boiled, without salt |  |
| Lentilhas cozidas | [172421](https://fdc.nal.usda.gov/food-details/172421/nutrients) | Lentils, mature seeds, cooked, boiled, without salt |  |
| Ervilhas cozidas | [170017](https://fdc.nal.usda.gov/food-details/170017/nutrients) | Peas, green, frozen, cooked, boiled, drained, without salt | Congeladas e cozidas. |
| Favas cozidas | [173753](https://fdc.nal.usda.gov/food-details/173753/nutrients) | Broadbeans (fava beans), mature seeds, cooked, boiled, without salt |  |
| Tremoços cozidos | [172424](https://fdc.nal.usda.gov/food-details/172424/nutrients) | Lupins, mature seeds, cooked, boiled, without salt |  |
| Tofu firme | [172475](https://fdc.nal.usda.gov/food-details/172475/nutrients) | Tofu, raw, firm, prepared with calcium sulfate |  |
| Edamame | [168411](https://fdc.nal.usda.gov/food-details/168411/nutrients) | Edamame, frozen, prepared |  |
| Húmus | [174289](https://fdc.nal.usda.gov/food-details/174289/nutrients) | Hummus, commercial |  |
| Brócolos crus | [170379](https://fdc.nal.usda.gov/food-details/170379/nutrients) | Broccoli, raw |  |
| Couve-flor crua | [169986](https://fdc.nal.usda.gov/food-details/169986/nutrients) | Cauliflower, raw |  |
| Espinafres crus | [168462](https://fdc.nal.usda.gov/food-details/168462/nutrients) | Spinach, raw |  |
| Alface crua | [169249](https://fdc.nal.usda.gov/food-details/169249/nutrients) | Lettuce, green leaf, raw |  |
| Rúcula crua | [169387](https://fdc.nal.usda.gov/food-details/169387/nutrients) | Arugula, raw |  |
| Tomate | [170457](https://fdc.nal.usda.gov/food-details/170457/nutrients) | Tomatoes, red, ripe, raw, year round average |  |
| Cenoura crua | [170393](https://fdc.nal.usda.gov/food-details/170393/nutrients) | Carrots, raw |  |
| Cebola crua | [170000](https://fdc.nal.usda.gov/food-details/170000/nutrients) | Onions, raw |  |
| Alho cru | [169230](https://fdc.nal.usda.gov/food-details/169230/nutrients) | Garlic, raw |  |
| Pepino | [168409](https://fdc.nal.usda.gov/food-details/168409/nutrients) | Cucumber, with peel, raw | Com casca. |
| Pimento vermelho | [170108](https://fdc.nal.usda.gov/food-details/170108/nutrients) | Peppers, sweet, red, raw |  |
| Pimento verde | [170427](https://fdc.nal.usda.gov/food-details/170427/nutrients) | Peppers, sweet, green, raw |  |
| Curgete crua | [169291](https://fdc.nal.usda.gov/food-details/169291/nutrients) | Squash, summer, zucchini, includes skin, raw | Com casca. |
| Beringela crua | [169228](https://fdc.nal.usda.gov/food-details/169228/nutrients) | Eggplant, raw |  |
| Abóbora crua | [168448](https://fdc.nal.usda.gov/food-details/168448/nutrients) | Pumpkin, raw |  |
| Feijão-verde cru | [169961](https://fdc.nal.usda.gov/food-details/169961/nutrients) | Beans, snap, green, raw |  |
| Cogumelos crus | [169251](https://fdc.nal.usda.gov/food-details/169251/nutrients) | Mushrooms, white, raw | Cogumelo branco (champignon). |
| Couve-galega crua | [170406](https://fdc.nal.usda.gov/food-details/170406/nutrients) | Collards, raw |  |
| Couve lombarda crua | [170388](https://fdc.nal.usda.gov/food-details/170388/nutrients) | Cabbage, savoy, raw |  |
| Repolho cru | [169975](https://fdc.nal.usda.gov/food-details/169975/nutrients) | Cabbage, raw |  |
| Espargos crus | [168389](https://fdc.nal.usda.gov/food-details/168389/nutrients) | Asparagus, raw |  |
| Beterraba crua | [169145](https://fdc.nal.usda.gov/food-details/169145/nutrients) | Beets, raw |  |
| Alho-francês cru | [169246](https://fdc.nal.usda.gov/food-details/169246/nutrients) | Leeks, (bulb and lower leaf-portion), raw |  |
| Nabo cru | [170465](https://fdc.nal.usda.gov/food-details/170465/nutrients) | Turnips, raw |  |
| Grelos crus | [170381](https://fdc.nal.usda.gov/food-details/170381/nutrients) | Broccoli raab, raw |  |
| Banana | [173944](https://fdc.nal.usda.gov/food-details/173944/nutrients) | Bananas, raw |  |
| Maçã | [171688](https://fdc.nal.usda.gov/food-details/171688/nutrients) | Apples, raw, with skin (Includes foods for USDA's Food Distribution Program) | Com casca. |
| Pera | [169118](https://fdc.nal.usda.gov/food-details/169118/nutrients) | Pears, raw |  |
| Laranja | [169097](https://fdc.nal.usda.gov/food-details/169097/nutrients) | Oranges, raw, all commercial varieties |  |
| Tangerina | [169105](https://fdc.nal.usda.gov/food-details/169105/nutrients) | Tangerines, (mandarin oranges), raw |  |
| Morangos | [167762](https://fdc.nal.usda.gov/food-details/167762/nutrients) | Strawberries, raw |  |
| Uvas | [174683](https://fdc.nal.usda.gov/food-details/174683/nutrients) | Grapes, red or green (European type, such as Thompson seedless), raw |  |
| Kiwi | [168153](https://fdc.nal.usda.gov/food-details/168153/nutrients) | Kiwifruit, green, raw |  |
| Ananás | [169124](https://fdc.nal.usda.gov/food-details/169124/nutrients) | Pineapple, raw, all varieties |  |
| Melancia | [167765](https://fdc.nal.usda.gov/food-details/167765/nutrients) | Watermelon, raw |  |
| Melão | [169911](https://fdc.nal.usda.gov/food-details/169911/nutrients) | Melons, honeydew, raw |  |
| Pêssego | [169928](https://fdc.nal.usda.gov/food-details/169928/nutrients) | Peaches, yellow, raw |  |
| Manga | [169910](https://fdc.nal.usda.gov/food-details/169910/nutrients) | Mangos, raw |  |
| Mirtilos | [171711](https://fdc.nal.usda.gov/food-details/171711/nutrients) | Blueberries, raw |  |
| Framboesas | [167755](https://fdc.nal.usda.gov/food-details/167755/nutrients) | Raspberries, raw |  |
| Abacate | [171705](https://fdc.nal.usda.gov/food-details/171705/nutrients) | Avocados, raw, all commercial varieties |  |
| Limão | [167746](https://fdc.nal.usda.gov/food-details/167746/nutrients) | Lemons, raw, without peel | Sem casca. |
| Cerejas | [171719](https://fdc.nal.usda.gov/food-details/171719/nutrients) | Cherries, sweet, raw |  |
| Figos frescos | [173021](https://fdc.nal.usda.gov/food-details/173021/nutrients) | Figs, raw |  |
| Ameixa | [169949](https://fdc.nal.usda.gov/food-details/169949/nutrients) | Plums, raw |  |
| Alperce | [171697](https://fdc.nal.usda.gov/food-details/171697/nutrients) | Apricots, raw |  |
| Tâmaras | [168191](https://fdc.nal.usda.gov/food-details/168191/nutrients) | Dates, medjool | Variedade medjool. |
| Passas | [168165](https://fdc.nal.usda.gov/food-details/168165/nutrients) | Raisins, dark, seedless (Includes foods for USDA's Food Distribution Program) |  |
| Amêndoas | [170567](https://fdc.nal.usda.gov/food-details/170567/nutrients) | Nuts, almonds |  |
| Nozes | [170187](https://fdc.nal.usda.gov/food-details/170187/nutrients) | Nuts, walnuts, english |  |
| Cajus | [170162](https://fdc.nal.usda.gov/food-details/170162/nutrients) | Nuts, cashew nuts, raw |  |
| Avelãs | [170581](https://fdc.nal.usda.gov/food-details/170581/nutrients) | Nuts, hazelnuts or filberts |  |
| Amendoins | [172430](https://fdc.nal.usda.gov/food-details/172430/nutrients) | Peanuts, all types, raw |  |
| Manteiga de amendoim | [172470](https://fdc.nal.usda.gov/food-details/172470/nutrients) | Peanut butter, smooth style, without salt | Sem sal. |
| Pistácios | [170184](https://fdc.nal.usda.gov/food-details/170184/nutrients) | Nuts, pistachio nuts, raw |  |
| Sementes de chia | [170554](https://fdc.nal.usda.gov/food-details/170554/nutrients) | Seeds, chia seeds, dried |  |
| Sementes de linhaça | [169414](https://fdc.nal.usda.gov/food-details/169414/nutrients) | Seeds, flaxseed |  |
| Sementes de abóbora | [170556](https://fdc.nal.usda.gov/food-details/170556/nutrients) | Seeds, pumpkin and squash seed kernels, dried |  |
| Sementes de girassol | [170562](https://fdc.nal.usda.gov/food-details/170562/nutrients) | Seeds, sunflower seed kernels, dried |  |
| Azeite | [171413](https://fdc.nal.usda.gov/food-details/171413/nutrients) | Oil, olive, salad or cooking |  |
| Óleo de girassol | [171025](https://fdc.nal.usda.gov/food-details/171025/nutrients) | Oil, sunflower, linoleic, (approx. 65%) |  |
| Óleo de coco | [171412](https://fdc.nal.usda.gov/food-details/171412/nutrients) | Oil, coconut |  |
| Azeitonas | [169094](https://fdc.nal.usda.gov/food-details/169094/nutrients) | Olives, ripe, canned (small-extra large) |  |
| Mel | [169640](https://fdc.nal.usda.gov/food-details/169640/nutrients) | Honey |  |
| Açúcar | [169655](https://fdc.nal.usda.gov/food-details/169655/nutrients) | Sugars, granulated | Açúcar branco. |
| Compota | [169641](https://fdc.nal.usda.gov/food-details/169641/nutrients) | Jams and preserves |  |
| Chocolate negro 70-85% | [170273](https://fdc.nal.usda.gov/food-details/170273/nutrients) | Chocolate, dark, 70-85% cacao solids |  |
| Cacau em pó | [169593](https://fdc.nal.usda.gov/food-details/169593/nutrients) | Cocoa, dry powder, unsweetened | Sem açúcar. |
