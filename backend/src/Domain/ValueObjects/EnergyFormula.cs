namespace Domain.ValueObjects;

/// <summary>Fórmulas energéticas disponíveis para seleção pelo personal trainer.</summary>
public enum EnergyFormula
{
    HarrisBenedict,
    MifflinStJeor,
    Cunningham,
    Tinsley
}

public static class EnergyFormulaExtensions
{
    public static string ToKey(this EnergyFormula formula) => formula switch
    {
        EnergyFormula.HarrisBenedict => "harris_benedict",
        EnergyFormula.MifflinStJeor => "mifflin_st_jeor",
        EnergyFormula.Cunningham => "cunningham",
        EnergyFormula.Tinsley => "tinsley",
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
}
