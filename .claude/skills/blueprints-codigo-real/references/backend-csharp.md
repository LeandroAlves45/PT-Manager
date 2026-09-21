# Referência — backend C# / Clean Architecture

Complementa `SKILL.md`. Ler quando o ficheiro documentado é `backend/src/**` ou
`backend/tests/**`.

## Camada e responsabilidade

Domain, Application, Infrastructure ou Api, e porquê essa e não outra.

## O bloco de código inclui, quando aplicável

1. Apenas os `using` necessários.
2. Namespace coerente com o caminho real.
3. XML Docs nos limites públicos relevantes.
4. Tipo, propriedades, campos, construtor e métodos completos.
5. Dependency Injection explícita e validação das dependências.
6. `CancellationToken` propagado em todas as operações assíncronas de I/O.
7. `Result` ou `Result<T>` para falhas esperadas da Application.
8. Comentários apenas quando explicam o motivo de uma decisão não evidente.

## Regras arquitecturais

1. Domain não depende de Application, Infrastructure ou Api.
2. Application depende apenas de Domain e define as portas necessárias.
3. Infrastructure implementa as portas da Application.
4. Api permanece o composition root.
5. Não usar MediatR, AutoMapper, repository genérico ou Unit of Work genérico.
6. O tenant efectivo vem de `ITenantContext`; nunca de um identificador recebido no
   payload, route ou query string.
7. Mapping de DTOs é explícito.
8. Validators usam FluentValidation core e são chamados explicitamente.

## Validação obrigatória específica de backend

Além da lista comum em `SKILL.md`:

1. Verificar usings, namespaces, nulabilidade e assinaturas contra o código actual.
2. Verificar que os testes documentados provam comportamento e não implementação.
3. Comparar cada bloco com o desenho funcional ou pseudocódigo aprovado.
4. Em lotes com múltiplos ficheiros dependentes, materializar todos os blocos numa cópia
   temporária do projecto e executar build, testes unitários e testes de arquitectura.
   Testes de infraestrutura devem pelo menos compilar quando a execução depender de uma
   migration deliberadamente diferida.
5. Declarar o budget de queries por caso de uso — joins esperados, paginação, ordenação e
   índice de suporte. Provar budgets críticos com `DbCommandInterceptor` quando o risco de
   N+1 ou round-trip redundante existir. Quando o índice de suporte declarado exigir uma
   ordem de colunas líderes, confirmar que o `OrderBy`/`ThenBy` documentado a respeita, ou
   justificar a divergência na nota de mentor.
6. Quando existem DTOs por actor, manter uma matriz explícita de exposição de campos e
   testes negativos para dados internos.
7. Confirmar através do estado Git que nenhum ficheiro real foi implementado.
8. Ficheiros de composição grandes e transversais (`DbContext`, `DependencyInjection`,
   interceptors) nunca se resumem para poupar espaço no documento; o bloco tem de conter a
   íntegra real com os deltas assinalados na nota de mentor.

## Duas lições pagas a manter presentes

Estas duas regras nasceram de incidentes reais neste projecto e continuam a valer porque o
tipo de erro que previnem não é óbvio à primeira leitura.

**Composição grande apresentada como esqueleto.** Num lote da Sprint 3, `PtManagerDbContext.cs`
e `DependencyInjection.cs` foram apresentados como esqueletos de ~15 linhas por um agente
diferente (`Sessions/2026-08-20-lote-3e-blueprints-e-correcao-copilot.md`). Um ficheiro de
composição real já existente nunca é "a criar": é sempre a íntegra real mais os deltas.

**Helper partilhado que passa a validar algo antes ausente.** Ao extrair um helper de
autorização que passou a exigir um campo antes opcional (`ITenantContext.UserId` obrigatório
em `ActorAuthorization.RequireTrainer`), 72 de 134 testes de quatro features falharam a
*runtime* com um erro genérico que mascarava a asserção original
(`Sessions/2026-08-20-auditoria-autorizacao-clients-packs-training-nutrition.md`). Um test
double desactualizado não falha a compilar. Ao mudar a assinatura de uma porta usada por test
doubles, correr `dotnet test` de verdade em todas as features consumidoras, nunca confiar só
no compilador.

**Build incremental mascara mutações.** Restaurar um ficheiro com `mtime` antigo depois de uma
corrida de mutação engana o MSBuild, que dá o projecto por actualizado e reutiliza DLLs com o
código mutado — falhas sem qualquer resíduo no `git diff`. Guiões de mutação fazem `touch` no
restauro, ou a corrida seguinte usa `--no-incremental`.

## Exemplo real

Excerto do blueprint `docs/blueprints/backend-files/sprint_6/sprint_6B/01_domain_calendario_ciclico_e_adesao.md`,
validado e fechado nessa fase. Mostra o formato esperado: caminho, estado, XML Docs, nota de
mentor a justificar uma decisão não óbvia (o limite do ciclo de procura, que evita um loop
infinito), e a chamada para a validação.

### `backend/src/Domain/Services/TrainingPlanSchedule.cs`

**Estado:** a criar.

**Porquê:** as semanas contam-se alinhadas à segunda-feira a partir da semana de
`StartDate`, e `FindNextScheduledDate` percorre no máximo um ciclo completo — o que basta
para encontrar qualquer dia definido sem risco de ciclo infinito.

```csharp
namespace Domain.Services;

/// <summary>
/// Posição de uma data no calendário de um plano de treino: semana do plano (1..N) e dia da
/// semana no formato do schema (0 = segunda … 6 = domingo).
/// </summary>
public readonly record struct ScheduledSlot(int WeekNumber, int DayOfWeek);

/// <summary>
/// Converte datas locais em posições cíclicas de um plano de treino. Função pura, sem
/// relógio nem fuso: o chamador fornece a data já convertida para o dia local do trainer.
/// </summary>
public static class TrainingPlanSchedule
{
    public const int MaximumCycleLengthWeeks = 52;

    public static int ToPlanDayOfWeek(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-ToPlanDayOfWeek(date));

    public static ScheduledSlot? Resolve(
        DateOnly startDate, DateOnly? endDate, int cycleLengthWeeks, DateOnly date)
    {
        EnsureCycleLength(cycleLengthWeeks);
        if (date < startDate || endDate.HasValue && date > endDate.Value)
            return null;

        var weeksSinceStart = (WeekStart(date).DayNumber - WeekStart(startDate).DayNumber) / 7;
        return new ScheduledSlot(weeksSinceStart % cycleLengthWeeks + 1, ToPlanDayOfWeek(date));
    }

    /// <summary>
    /// Procura a primeira data com treino depois de <paramref name="afterDate"/>. Percorre no
    /// máximo um ciclo completo — o que basta para encontrar qualquer dia definido — para
    /// nunca entrar em loop infinito.
    /// </summary>
    public static DateOnly? FindNextScheduledDate(
        DateOnly startDate, DateOnly? endDate, int cycleLengthWeeks,
        DateOnly afterDate, IReadOnlySet<ScheduledSlot> scheduledSlots)
    {
        EnsureCycleLength(cycleLengthWeeks);
        ArgumentNullException.ThrowIfNull(scheduledSlots);
        if (scheduledSlots.Count == 0) return null;

        var candidate = afterDate < startDate ? startDate : afterDate.AddDays(1);
        var searchEnd = candidate.AddDays(cycleLengthWeeks * 7);

        for (; candidate < searchEnd; candidate = candidate.AddDays(1))
        {
            if (endDate.HasValue && candidate > endDate.Value) return null;
            var slot = Resolve(startDate, endDate, cycleLengthWeeks, candidate);
            if (slot.HasValue && scheduledSlots.Contains(slot.Value)) return candidate;
        }
        return null;
    }

    private static void EnsureCycleLength(int cycleLengthWeeks)
    {
        if (cycleLengthWeeks is < 1 or > MaximumCycleLengthWeeks)
            throw new ArgumentOutOfRangeException(nameof(cycleLengthWeeks), cycleLengthWeeks,
                "The training plan cycle must have between 1 and 52 weeks.");
    }
}
```

**Validação:** `TrainingPlanScheduleTests` cobre início, domingo da semana 1, primeira
segunda, reinício do ciclo, plano de uma semana, limites do plano e ciclo inválido.

Ficheiro completo (com XML Docs integrais) em
`docs/blueprints/backend-files/sprint_6/sprint_6B/01_domain_calendario_ciclico_e_adesao.md`.
