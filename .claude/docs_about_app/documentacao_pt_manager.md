# Documentação do PT Manager

## 1. Objetivo deste documento

Este documento define a documentação recomendada para o PT Manager, explica a finalidade de cada artefacto e estabelece uma ordem prática para os criar, mesmo com a aplicação já numa fase avançada de desenvolvimento.

A documentação deve descrever o produto real, incluindo funcionalidades implementadas, decisões já tomadas, limitações conhecidas e trabalho futuro. Não deve apresentar funcionalidades planeadas como se já estivessem implementadas.

## 2. Princípios de documentação

- **Documentar a realidade atual:** analisar o código, os testes, os endpoints, o modelo de dados e a interface antes de escrever afirmações sobre o sistema.
- **Separar o estado atual do estado pretendido:** identificar explicitamente o que está implementado, parcialmente implementado, planeado, descontinuado ou por validar.
- **Manter rastreabilidade:** relacionar requisitos, regras de negócio, casos de uso, critérios de aceitação, testes e implementação quando aplicável.
- **Evitar duplicação:** cada tipo de informação deve ter uma fonte de verdade. Outros documentos devem referenciá-la.
- **Documentar decisões relevantes:** registar a decisão, o contexto, as alternativas consideradas e as consequências.
- **Atualizar com o código:** alterações relevantes ao comportamento, à arquitetura ou às permissões devem desencadear uma revisão da documentação.
- **Não criar burocracia:** documentos pequenos e úteis são preferíveis a documentos extensos que ficam desatualizados.
- **Não inventar requisitos:** dúvidas e pressupostos devem ficar assinalados como pendentes de validação.

## 3. Estado de cada elemento

Utilizar um dos seguintes estados nos inventários e nos requisitos:

- `Implementado`: confirmado no código e, quando aplicável, validado por testes.
- `Parcialmente implementado`: existe uma parte da funcionalidade, mas falta completar ou validar.
- `Planeado`: aprovado para desenvolvimento futuro, mas ainda não implementado.
- `Por validar`: existe uma hipótese ou decisão que precisa de confirmação.
- `Descontinuado`: já não faz parte do âmbito atual, mas pode ser relevante para o histórico.

Não classificar uma funcionalidade como implementada apenas porque existe um ecrã, um modelo ou um endpoint. Confirmar o percurso completo e os controlos relevantes.

## 4. Documentos recomendados

### 4.1. Visão do produto e âmbito

**Nome sugerido:** `docs/01-product/product-vision.md`

**Finalidade:** explicar o problema que o PT Manager procura resolver, para quem, qual o valor esperado e o que pertence ao âmbito do produto.

**Deve incluir:**
- Resumo do produto.
- Problema a resolver e evidência disponível.
- Público-alvo e stakeholders.
- Objetivos do produto.
- Proposta de valor.
- Âmbito atual.
- Funcionalidades fora do âmbito.
- Pressupostos, limitações e questões em aberto.
- Estado atual do produto e objetivo da próxima versão.

**Aplicação ao PT Manager:** descrever as necessidades de gestão de clientes, planos de treino, acompanhamento, packs e sessões apenas na medida em que correspondam ao âmbito confirmado do produto.

**Prioridade:** muito alta. Criar primeiro, mesmo que seja necessário rever o documento após a análise do código.

### 4.2. Análise do problema e necessidades dos utilizadores

**Nome sugerido:** `docs/01-product/problem-analysis.md`

**Finalidade:** registar os problemas concretos, quem é afetado, como são resolvidos atualmente e que evidência sustenta a necessidade de cada funcionalidade.

**Deve incluir:**
- Problema observado.
- Stakeholder afetado.
- Processo atual.
- Consequências do problema.
- Solução atual ou alternativa utilizada.
- Evidência ou origem da informação.
- Necessidade identificada.
- Questões ainda por validar.

Distinguir factos observados de hipóteses. Não afirmar que os utilizadores têm determinado problema sem evidência ou validação.

**Prioridade:** alta. Reconstruir a partir do conhecimento do produto e validar com utilizadores quando possível.

### 4.3. Stakeholders, atores e roles

**Nome sugerido:** `docs/02-requirements/stakeholders-and-actors.md`

**Finalidade:** identificar quem utiliza, gere, mantém ou é afetado pela aplicação.

**Deve incluir:**
- Stakeholder ou ator.
- Objetivos e responsabilidades.
- Necessidades principais.
- Interações com o sistema.
- Limites de responsabilidade.
- Dependências e interesses relevantes.

Distinguir **stakeholders** de **atores**. Um stakeholder pode influenciar ou ser afetado pelo sistema sem interagir diretamente com ele. Um ator participa numa interação com o sistema.

**Aplicação ao PT Manager:** confirmar os roles reais, por exemplo treinador, cliente e administradores, sem presumir que todos têm de existir na primeira versão.

**Prioridade:** muito alta.

### 4.4. Requisitos funcionais

**Nome sugerido:** `docs/02-requirements/functional-requirements.md`

**Finalidade:** especificar os comportamentos e serviços que o sistema deve disponibilizar.

**Cada requisito deve ter:**
- Identificador único, por exemplo `RF-001`.
- Nome curto.
- Descrição clara e verificável.
- Ator ou atores envolvidos.
- Pré-condições, se existirem.
- Resultado esperado.
- Prioridade.
- Estado de implementação.
- Critério de aceitação ou referência para os critérios.
- Referências a regras de negócio e casos de uso relacionados.

**Exemplo ilustrativo:**

`RF-001 — Consultar clientes`

O sistema deve permitir ao treinador consultar a lista de clientes abrangidos pela sua autorização.

Critério de aceitação: a resposta contém apenas clientes que o treinador está autorizado a consultar.

O exemplo não confirma que o comportamento atual está implementado corretamente. Isso deve ser verificado no código e nos testes.

**Prioridade:** muito alta. Reconstruir a partir das funcionalidades existentes e do âmbito aprovado.

### 4.5. Requisitos não funcionais e restrições

**Nome sugerido:** `docs/02-requirements/non-functional-requirements.md`

**Finalidade:** definir atributos de qualidade, requisitos operacionais e restrições técnicas ou legais relevantes.

**Áreas a avaliar:**
- Segurança e controlo de acesso.
- Privacidade e proteção de dados.
- Desempenho e capacidade.
- Disponibilidade e recuperação.
- Fiabilidade e integridade dos dados.
- Usabilidade e acessibilidade.
- Compatibilidade com navegadores e dispositivos.
- Manutenibilidade e testabilidade.
- Logging, monitorização e auditoria.
- Backup e recuperação.
- Restrições tecnológicas, de alojamento ou orçamento.

Os requisitos devem ser verificáveis sempre que possível. Evitar expressões vagas como “rápido”, “seguro” ou “fácil de usar” sem definir como serão avaliadas.

**Exemplo ilustrativo:** “Os endpoints que devolvem dados de clientes devem verificar no servidor se o utilizador autenticado tem autorização para aceder aos recursos pedidos.”

Não definir metas numéricas de desempenho ou disponibilidade sem compreender as necessidades e a infraestrutura.

**Prioridade:** muito alta, sobretudo antes de disponibilizar a aplicação a utilizadores reais.

### 4.6. Regras de negócio

**Nome sugerido:** `docs/02-requirements/business-rules.md`

**Finalidade:** documentar as condições que regem o funcionamento do produto, independentemente do ecrã ou da tecnologia.

**Cada regra deve ter:**
- Identificador único, por exemplo `RN-001`.
- Nome.
- Formulação inequívoca.
- Condições em que se aplica.
- Resultado quando a regra é satisfeita ou violada.
- Exceções, se existirem.
- Estado e origem da decisão.
- Requisitos, casos de uso e testes relacionados.

**Exemplos de temas a validar no PT Manager:**
- Elegibilidade, validade e consumo de packs.
- Condições para agendar, cancelar ou concluir sessões.
- Prevenção de sessões duplicadas.
- Renovação de packs e tratamento de packs arquivados.
- Regras de arquivamento de clientes.
- Relação entre treinador, cliente e propriedade dos dados.
- Regras para alterações a planos e avaliações.

Não assumir que uma regra de exemplo é a regra final. Confirmar o comportamento desejado com base no código existente e nas decisões de produto.

**Prioridade:** muito alta. É um dos documentos mais importantes para evitar comportamentos inconsistentes.

### 4.7. Casos de uso

**Nome sugerido:** `docs/03-functional-design/use-cases.md`

**Finalidade:** descrever como um ator alcança um objetivo através do sistema.

**Cada caso de uso deve conter:**
- Identificador, por exemplo `UC-001`.
- Nome e objetivo.
- Ator principal e atores secundários.
- Pré-condições.
- Fluxo principal.
- Fluxos alternativos.
- Tratamento de erros e exceções.
- Pós-condições.
- Requisitos e regras de negócio relacionados.
- Estado de implementação.

**Casos de uso candidatos a avaliar no PT Manager:**
- Autenticar utilizador.
- Criar ou atualizar cliente.
- Consultar detalhes e histórico do cliente.
- Criar ou renovar pack.
- Agendar, cancelar ou concluir sessão.
- Criar ou atualizar plano de treino.
- Registar avaliação física ou check-in.
- Consultar dados pessoais como cliente.
- Gerir utilizadores, se existir essa responsabilidade administrativa.

Incluir apenas os casos que pertencem ao âmbito confirmado.

**Prioridade:** alta. Começar pelos fluxos críticos e com mais regras ou permissões.

### 4.8. Fluxos de trabalho e diagramas

**Nome sugerido:** `docs/03-functional-design/workflows.md`

**Finalidade:** representar visualmente os passos, decisões, exceções e responsabilidades de um processo.

Usar:
- **Diagrama de atividades UML:** para fluxos com decisões, caminhos alternativos e tratamento de erros.
- **Swimlanes:** quando é importante distinguir ações do treinador, cliente, backend ou serviços externos.
- **Diagrama de casos de uso UML:** para mostrar atores e funcionalidades a alto nível.
- **Diagrama de sequência:** quando for necessário mostrar a interação temporal entre frontend, API, base de dados e serviços externos.

Não é necessário desenhar todos os fluxos. Priorizar processos críticos, complexos ou sujeitos a erros. Cada diagrama deve ter título, objetivo e ligação aos casos de uso ou requisitos relevantes.

**Prioridade:** alta para processos críticos; média para fluxos simples.

### 4.9. Matriz de permissões

**Nome sugerido:** `docs/03-functional-design/permissions-matrix.md`

**Finalidade:** definir o que cada role pode fazer e sobre que recursos.

**Deve considerar:**
- Role.
- Recurso.
- Operação: consultar, criar, atualizar, arquivar ou eliminar.
- Âmbito dos dados: próprios dados, clientes atribuídos, todos os recursos autorizados, entre outros.
- Restrições e exceções.
- Local de aplicação e testes associados.

Não usar apenas uma tabela com permissões genéricas. Distinguir a permissão para executar uma operação da autorização para aceder a um registo concreto.

A interface pode ocultar ações não autorizadas, mas o backend tem de validar a autorização em todos os pedidos relevantes. Aplicar o princípio do menor privilégio e negar por omissão.

**Prioridade:** muito alta, sobretudo se houver vários treinadores, clientes e administradores.

### 4.10. Critérios de aceitação

**Nome sugerido:** `docs/05-quality/acceptance-criteria.md`

**Finalidade:** tornar explícito o que tem de acontecer para uma funcionalidade ser considerada concluída.

Os critérios devem ser objetivos, verificáveis e orientados ao comportamento observável. Podem usar o formato Dado/Quando/Então (Given/When/Then).

**Exemplo ilustrativo:**
- Dado que um utilizador não tem autorização para consultar um cliente.
- Quando tenta obter os detalhes desse cliente através da API.
- Então o sistema deve recusar o acesso sem expor os dados protegidos.

Relacionar os critérios com RF, regras de negócio e casos de teste. Evitar repetir o requisito integralmente em vários documentos.

**Prioridade:** muito alta para funcionalidades críticas e para novas alterações.

### 4.11. Estratégia e plano de testes

**Nome sugerido:** `docs/05-quality/test-strategy.md`

**Finalidade:** explicar como será validada a qualidade do sistema.

**Deve incluir:**
- Âmbito dos testes.
- Estratégia de testes unitários, integração e ponta a ponta.
- Testes de autorização e segurança.
- Testes de regras de negócio.
- Gestão de dados de teste.
- Critérios para considerar uma alteração pronta.
- Execução local e em CI.
- Lacunas de cobertura e riscos conhecidos.

A documentação não substitui os testes executáveis. Os testes devem existir no projeto e validar comportamentos reais. Não declarar que um requisito está validado se não houver evidência suficiente.

**Prioridade:** muito alta.

### 4.12. Roadmap e backlog

**Nomes sugeridos:**
- `docs/01-product/roadmap.md`
- `docs/06-project-management/backlog.md`, caso o backlog não seja gerido noutra ferramenta.

**Finalidade:** o roadmap comunica a direção e as fases do produto. O backlog regista unidades de trabalho concretas e priorizadas.

**Roadmap:**
- Objetivos por fase.
- Capacidades previstas.
- Dependências e riscos.
- Critérios de conclusão da fase.
- Estado e horizonte temporal, evitando datas sem fundamento.

**Backlog:**
- Identificador.
- Descrição.
- Tipo: funcionalidade, melhoria, bug, dívida técnica ou investigação.
- Prioridade.
- Estado.
- Critérios de aceitação.
- Dependências.
- Requisitos relacionados.

MoSCoW pode ajudar a classificar itens como Must, Should, Could e Won't for now. Não confundir prioridade com ordem de implementação: dependências técnicas e riscos também influenciam a sequência.

**Prioridade:** alta. Rever o backlog atual antes de criar outro, para evitar duplicação.

### 4.13. Arquitetura e decisões técnicas

**Nomes sugeridos:**
- `docs/04-architecture/architecture.md`
- `docs/04-architecture/data-model.md`
- `docs/04-architecture/decisions/`

**Finalidade:** explicar a organização técnica, as responsabilidades dos componentes e as decisões com impacto significativo.

**Arquitetura:**
- Contexto do sistema e utilizadores externos.
- Componentes principais e responsabilidades.
- Fluxos entre frontend, backend, base de dados e serviços externos.
- Ambientes e processo de deploy.
- Fronteiras de segurança e dependências relevantes.

**Modelo de dados:**
- Entidades e respetivas responsabilidades.
- Relações, cardinalidades e restrições.
- Chaves, índices e integridade.
- Regras de retenção ou eliminação quando aplicáveis.
- Diferenças relevantes entre o modelo documentado e o esquema real.

**Registo de decisões arquiteturais (ADR):**
- Contexto e problema.
- Decisão tomada.
- Alternativas consideradas.
- Consequências positivas e negativas.
- Estado da decisão e data.

Criar ADRs para decisões significativas, como autenticação, autorização, persistência, organização da arquitetura ou escolha de serviços. Não criar um ADR para cada detalhe trivial.

**Prioridade:** alta. Documentar a arquitetura que existe antes de propor uma arquitetura futura.

### 4.14. Problemas, riscos e dívida técnica

**Nome sugerido:** `docs/06-project-management/issues-and-risks.md`

**Finalidade:** manter visíveis os problemas conhecidos, os riscos e a dívida técnica.

**Para cada entrada:**
- Identificador.
- Tipo: bug, risco, dívida técnica ou questão em aberto.
- Descrição e impacto.
- Evidência ou forma de reprodução.
- Probabilidade e gravidade, quando aplicável.
- Mitigação ou ação proposta.
- Prioridade, responsável e estado.
- Ligação a tarefas, requisitos ou decisões.

Se os bugs já forem geridos num issue tracker, usar essa ferramenta como fonte de verdade e manter aqui apenas os riscos e decisões que precisem de contexto transversal.

**Prioridade:** alta.

### 4.15. Privacidade, segurança e dados

**Nome sugerido:** `docs/05-quality/security-and-data-protection.md`

**Finalidade:** registar os dados tratados, os riscos de segurança e os controlos relevantes.

**Deve incluir, conforme aplicável:**
- Categorias de dados recolhidos.
- Finalidade e necessidade de cada categoria.
- Atores com acesso e respetivo âmbito.
- Dados pessoais e potencialmente sensíveis, incluindo informações sobre saúde ou condição física.
- Retenção, exportação, correção e eliminação.
- Autenticação, autorização, gestão de sessões e segredos.
- Logs, backups, incidentes e acessos administrativos.
- Serviços externos que recebem dados.
- Questões de privacidade e obrigações legais a confirmar.

Como o PT Manager pode tratar medidas corporais, fotografias de progresso e informações relacionadas com saúde, avaliar cuidadosamente a aplicação do RGPD e das regras aplicáveis a categorias especiais de dados. Não assumir que todos estes dados têm automaticamente a mesma classificação jurídica; a avaliação depende da natureza, finalidade e contexto do tratamento. Procurar aconselhamento especializado quando necessário.

**Prioridade:** muito alta antes de tratar dados reais de clientes.

### 4.16. README e instruções de desenvolvimento

**Nome sugerido:** `README.md` na raiz do repositório e, se necessário, documentação complementar em `docs/`.

**Finalidade:** permitir que uma pessoa compreenda o projeto e o consiga executar.

**Deve incluir:**
- Descrição curta do projeto e estado atual.
- Stack tecnológica confirmada.
- Pré-requisitos.
- Configuração local.
- Variáveis de ambiente necessárias, sem segredos reais.
- Comandos para executar frontend e backend.
- Comandos para executar testes e verificações.
- Referências à documentação principal.
- Limitações ou avisos de desenvolvimento.

Não guardar tokens, passwords, chaves ou valores reais de configuração sensível no repositório.

**Prioridade:** alta.

## 5. Estrutura de diretórios sugerida

Adaptar a estrutura ao repositório existente. Não mover código nem criar ficheiros em massa antes de inspecionar a organização atual.

```text
docs/
├── 01-product/
│   ├── product-vision.md
│   ├── problem-analysis.md
│   └── roadmap.md
├── 02-requirements/
│   ├── stakeholders-and-actors.md
│   ├── functional-requirements.md
│   ├── non-functional-requirements.md
│   └── business-rules.md
├── 03-functional-design/
│   ├── use-cases.md
│   ├── workflows.md
│   └── permissions-matrix.md
├── 04-architecture/
│   ├── architecture.md
│   ├── data-model.md
│   └── decisions/
├── 05-quality/
│   ├── acceptance-criteria.md
│   ├── test-strategy.md
│   └── security-and-data-protection.md
└── 06-project-management/
    ├── backlog.md
    └── issues-and-risks.md
```

Esta estrutura é uma proposta inicial. Se já existirem documentos, convenções ou ferramentas equivalentes, avaliar a sua reutilização antes de criar novos ficheiros.

## 6. Ordem de execução recomendada para uma aplicação avançada

Não tentar preencher todos os documentos de uma vez. Trabalhar por etapas, começando pela descoberta do estado real do sistema.

### Etapa 1 — Inventário e descoberta

1. Inspecionar o repositório e identificar a estrutura de frontend, backend, testes, migrations, configuração e deploy.
2. Identificar funcionalidades existentes a partir de rotas, páginas, componentes, serviços, modelos, validações e testes.
3. Identificar roles, autenticação, autorização e regras já implementadas.
4. Identificar integrações, variáveis de ambiente, dependências e limitações conhecidas.
5. Comparar a implementação com o README e a documentação que já exista.
6. Registar dúvidas sem tentar resolvê-las através de suposições.

**Entregável:** inventário do estado atual, com evidência e lacunas.

### Etapa 2 — Reconstrução funcional

1. Criar ou atualizar a visão do produto e o âmbito.
2. Identificar stakeholders e atores.
3. Inventariar funcionalidades por módulo.
4. Reconstruir requisitos funcionais e não funcionais.
5. Extrair regras de negócio do código, testes e decisões conhecidas.
6. Marcar como pendentes as regras cujo comportamento pretendido não esteja claro.

**Entregável:** documentação funcional coerente com o produto existente, distinguindo implementação de intenção.

### Etapa 3 — Permissões e fluxos críticos

1. Construir a matriz de permissões.
2. Mapear os principais casos de uso.
3. Documentar os fluxos com maior complexidade ou risco.
4. Relacionar cada fluxo com requisitos, regras e critérios de aceitação.
5. Identificar testes ausentes, sobretudo para permissões e transições de estado.

**Entregável:** modelo funcional suficientemente claro para orientar alterações sem quebrar regras existentes.

### Etapa 4 — Arquitetura e qualidade

1. Documentar a arquitetura atual.
2. Documentar o modelo de dados real.
3. Registar decisões arquiteturais relevantes.
4. Definir a estratégia de testes e critérios de aceitação.
5. Rever segurança, privacidade, retenção e tratamento de dados.
6. Identificar riscos e dívida técnica.

**Entregável:** documentação técnica e de qualidade alinhada com o código.

### Etapa 5 — Planeamento futuro

1. Comparar o estado atual com os objetivos do produto.
2. Identificar lacunas, bugs e dívida técnica.
3. Priorizar o trabalho por valor, risco e dependências.
4. Definir o roadmap e manter um backlog único.
5. Associar critérios de aceitação às tarefas que vão ser iniciadas.

**Entregável:** plano de evolução baseado no estado real, não em pressupostos.

## 7. Como trabalhar com o Cursor

O Cursor deve ser usado como assistente para inspecionar e documentar o código existente, não como fonte automática de verdade. A análise gerada deve ser validada contra a implementação e os testes.

### Regras de trabalho

- Começar por pedir análise, não alterações.
- Não permitir que a ferramenta invente funcionalidades ou regras.
- Exigir referências a ficheiros, classes, funções, endpoints e testes quando afirmar que algo existe.
- Distinguir evidência direta, inferência e questão pendente.
- Não alterar código de aplicação durante a fase inicial de documentação.
- Não criar ou substituir documentação existente sem comparar primeiro o seu conteúdo.
- Gerar os documentos por etapas, revendo cada conjunto antes de avançar.
- Nunca incluir segredos reais, tokens ou dados pessoais de clientes na documentação.

### Prompt inicial para o Cursor

```text
Quero documentar o estado atual do projeto PT Manager antes de alterar
ou reorganizar o código.

Nesta fase, faz apenas análise. Não alteres código, configuração,
dependências, migrations, testes ou ficheiros existentes.

Objetivos:
1. Inspecionar a estrutura completa do repositório relevante.
2. Identificar os módulos e funcionalidades existentes.
3. Identificar páginas, componentes, rotas, endpoints, serviços,
   modelos de dados, validações e testes.
4. Identificar os mecanismos de autenticação e autorização e as
   verificações de acesso aos recursos.
5. Identificar regras de negócio que estejam explicitamente
   implementadas no código ou verificadas por testes.
6. Identificar integrações, configurações de execução e deploy,
   sem revelar valores de segredos.
7. Identificar inconsistências, funcionalidades incompletas e
   lacunas de testes ou documentação.
8. Identificar documentação já existente e convenções do repositório.

Regras:
- Não inventes requisitos, comportamentos ou decisões.
- Distingue factos comprovados, inferências e questões pendentes.
- Para cada afirmação técnica relevante, indica os caminhos dos
  ficheiros e, quando possível, os símbolos ou secções pertinentes.
- Não consideres uma funcionalidade completa apenas porque existe
  uma página, endpoint ou modelo.
- Não confundas o comportamento atual com o comportamento desejado.
- Não imprimas valores de variáveis de ambiente, credenciais,
  tokens, chaves ou dados pessoais.
- Não cries ainda os documentos finais.

Resultado:
Apresenta um inventário estruturado com:
A. Estrutura e stack tecnológica confirmadas.
B. Módulos e funcionalidades, com estado e evidência.
C. Roles e mecanismos de autorização identificados.
D. Regras de negócio identificadas e respetivas evidências.
E. Modelo de dados e relações.
F. Testes existentes e lacunas identificadas.
G. Arquitetura e integrações.
H. Problemas, riscos e questões pendentes.
I. Documentação existente que deve ser reutilizada.

No final, propõe uma ordem para produzir a documentação sem duplicar
informação. Aguarda validação antes de criar ou alterar ficheiros.
```

### Prompt para criar os documentos depois da validação

```text
Com base no inventário revisto e aprovado, cria a documentação do
PT Manager em Markdown dentro da pasta docs/, respeitando a estrutura
e as convenções já existentes no repositório.

Antes de criar cada ficheiro:
1. Verifica se já existe documentação equivalente.
2. Reutiliza ou propõe a atualização do ficheiro existente quando
   isso evitar duplicação.
3. Se houver conflitos ou decisões por validar, não inventes uma
   resolução. Regista a questão como pendente.

A documentação deve:
- Estar em português de Portugal.
- Distinguir estado implementado, parcialmente implementado,
  planeado, por validar e descontinuado.
- Incluir identificadores estáveis para requisitos e regras.
- Referenciar os ficheiros e testes que sustentam afirmações sobre
  o comportamento atual, sempre que possível.
- Relacionar requisitos, regras, casos de uso, critérios de aceitação
  e testes quando existir evidência suficiente.
- Distinguir requisitos confirmados de propostas futuras.
- Evitar repetir a mesma informação em vários documentos.
- Não conter credenciais, segredos ou dados reais de clientes.

Não alteres o código da aplicação. Cria apenas os documentos
aprovados e apresenta no final a lista dos ficheiros criados ou
atualizados, as questões por validar e as evidências em falta.
```

## 8. Checklist de conclusão

Considerar a primeira ronda de documentação concluída quando:

- [ ] A visão e o âmbito refletem o produto real.
- [ ] Os stakeholders e atores estão identificados.
- [ ] As funcionalidades têm requisitos e estados documentados.
- [ ] As regras de negócio importantes estão explícitas.
- [ ] As permissões estão definidas por role e âmbito dos dados.
- [ ] Os fluxos críticos têm casos de uso e critérios de aceitação.
- [ ] A arquitetura e o modelo de dados refletem a implementação.
- [ ] A estratégia de testes e as lacunas conhecidas estão registadas.
- [ ] Os riscos e as decisões importantes têm uma fonte de verdade.
- [ ] O roadmap distingue funcionalidades existentes de trabalho futuro.
- [ ] O README permite compreender como executar e testar o projeto.
- [ ] As questões não resolvidas estão identificadas sem pressupostos inventados.

## 9. Referências técnicas

Estas referências podem apoiar a elaboração e a revisão da documentação:

- ISO/IEC/IEEE 29148 — Engenharia de requisitos: https://www.iso.org/standard/72089.html
- C4 Model — documentação de arquitetura de software: https://c4model.com/
- The Scrum Guide — enquadramento de Product Backlog e planeamento Scrum: https://scrumguides.org/
- OWASP Authorization Cheat Sheet — práticas de autorização: https://cheatsheetseries.owasp.org/cheatsheets/Authorization_Cheat_Sheet.html
- OWASP Application Security Verification Standard (ASVS) — requisitos verificáveis de segurança: https://owasp.org/www-project-application-security-verification-standard/
- Comissão Europeia — proteção de dados e RGPD: https://commission.europa.eu/law/law-topic/data-protection/data-protection-eu_en

Confirmar sempre a versão e a aplicabilidade de cada referência ao contexto concreto do projeto.

## 10. Recomendação final

Para uma aplicação já avançada, o primeiro resultado não deve ser uma coleção de documentos escritos a partir da memória. Deve ser um inventário verificável da implementação atual. A partir desse inventário, reconstruir os requisitos e as regras, validar permissões e fluxos críticos e só depois organizar a evolução futura.

A documentação deve ajudar a tomar decisões, prevenir regressões, orientar testes e tornar o projeto compreensível para outra pessoa. Se um documento não cumprir uma destas funções, deve ser simplificado, integrado noutro ou eliminado.
