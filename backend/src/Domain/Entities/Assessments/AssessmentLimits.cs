namespace Domain.Entities.Assessments;

/// <summary> Limites partilhados pelas avaliações iniciais e check-ins. </summary>
public static class AssessmentLimits
{
    /// <summary>
    /// Peso corporal máximo aceite, em kg. A coluna é <c>numeric(10,2)</c>: sem este teto, um
    /// valor com mais de oito dígitos inteiros rebentava na escrita com um 500.
    /// </summary>
    public const decimal MaxWeightKg = 1000m;

    /// <summary>
    /// Casas decimais do peso e da massa gorda. As colunas são <c>numeric(10,2)</c>: com mais
    /// casas, o PostgreSQL arredondava ao gravar (0.004 passava a 0.00 e violava o check da
    /// tabela) e o reenvio do mesmo valor deixava de ser reconhecido como igual.
    /// </summary>
    public const int DecimalPlaces = 2;

    /// <summary>
    /// Medida corporal máxima aceite, em cm. Igual ao teto que o portal já usa no check-in: as
    /// medidas vivem em JSON e não rebentam a escrita, mas sem teto gravavam lixo.
    /// </summary>
    public const decimal MaxMeasurementCm = 1000m;

    /// <summary>Água média por dia, em litros, aceite na avaliação inicial.</summary>
    public const decimal MaxWaterLitersPerDay = 50m;

    /// <summary>Indica se o valor cabe nas casas decimais da coluna, sem arredondar.</summary>
    public static bool FitsDecimalPlaces(decimal value) =>
        decimal.Round(value, DecimalPlaces) == value;
}
