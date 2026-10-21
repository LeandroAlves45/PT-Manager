using System.Text.RegularExpressions;

namespace Domain.ValueObjects;

/// <summary>
/// Normalização das unidades escritas pelo personal trainer e pelo superuser nos suplementos.
/// «gr» conta como «g» e guarda-se sempre «g», em todos os papéis,
/// para o cliente não ver duas grafias da mesma unidade.
/// </summary>
public static class SupplementUnits
{
    // Só um número seguido de «gr» como palavra inteira: «5gr», «5 gr», «2,5 GR».
    // O \b final deixa intactos «5 grãos» e «5 grams» (o «ã»/«a» seguinte é letra, não fronteira).
    private static readonly Regex GramsAfterNumber = new(
        @"(?<=\d)(\s*)gr\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>Unidade de medida já sem espaços: «gr» (qualquer caixa) passa a «g».</summary>
    public static string NormalizeUnit(string trimmedUnit) =>
        string.Equals(trimmedUnit, "gr", StringComparison.OrdinalIgnoreCase) ? "g" : trimmedUnit;

    /// <summary>Dose em texto livre: «5gr» passa a «5g» e «5 gr» a «5 g»; o resto fica igual.</summary>
    public static string NormalizeServingSize(string trimmedServingSize) =>
        GramsAfterNumber.Replace(trimmedServingSize, "$1g");
}
