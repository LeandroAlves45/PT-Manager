namespace Domain.Entities.Assessments;

/// <summary> Limites partilhados pelas avaliações iniciais e check-ins. </summary>
public static class AssessmentLimits
{
    /// <summary>
    /// Peso corporal máximo aceite, em kg. A coluna é <c>numeric(10,2)</c>: sem este teto, um
    /// valor com mais de oito dígitos inteiros rebentava na escrita com um 500.
    /// </summary>
    public const decimal MaxWeightKg = 1000m;
}
