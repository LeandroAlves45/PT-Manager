using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Medidas antropométricas opcionais de um cliente, num único momento
/// (avaliação inicial ou checkIn). Todas as medidas são independentes ->
/// o personal trainer pode registar só as que fazem sentido para o seu cliente.
/// </summary>
public sealed record BodyMeasurements
{
    public decimal? WaistCm { get; private set; }
    public decimal? HipCm { get; private set; }
    public decimal? ChestCm { get; private set; }
    public decimal? RightArmCm { get; private set; }
    public decimal? LeftArmCm { get; private set; }
    public decimal? RightThighCm { get; private set; }
    public decimal? LeftThighCm { get; private set; }
    public decimal? RightCalfCm { get; private set; }
    public decimal? LeftCalfCm { get; private set; }

    private BodyMeasurements() { }

    /// <summary>
    /// Instância vazia -> todas as medidas por preencher.
    /// </summary>
    public static BodyMeasurements Empty { get; } =
        new BodyMeasurements(null, null, null, null, null, null, null, null, null);

    /// <summary>
    /// Cria um conjunto de medidas. Cada medida, quando presente, tem de ser positiva.
    /// </summary>
    public BodyMeasurements(
        decimal? waistCm,
        decimal? hipCm,
        decimal? chestCm,
        decimal? rightArmCm,
        decimal? leftArmCm,
        decimal? rightThighCm,
        decimal? leftThighCm,
        decimal? rightCalfCm,
        decimal? leftCalfCm)
    {
        ValidatePositiveWhenPresent(waistCm, nameof(waistCm));
        ValidatePositiveWhenPresent(hipCm, nameof(hipCm));
        ValidatePositiveWhenPresent(chestCm, nameof(chestCm));
        ValidatePositiveWhenPresent(rightArmCm, nameof(rightArmCm));
        ValidatePositiveWhenPresent(leftArmCm, nameof(leftArmCm));
        ValidatePositiveWhenPresent(rightThighCm, nameof(rightThighCm));
        ValidatePositiveWhenPresent(leftThighCm, nameof(leftThighCm));
        ValidatePositiveWhenPresent(rightCalfCm, nameof(rightCalfCm));
        ValidatePositiveWhenPresent(leftCalfCm, nameof(leftCalfCm));

        WaistCm = waistCm;
        HipCm = hipCm;
        ChestCm = chestCm;
        RightArmCm = rightArmCm;
        LeftArmCm = leftArmCm;
        RightThighCm = rightThighCm;
        LeftThighCm = leftThighCm;
        RightCalfCm = rightCalfCm;
        LeftCalfCm = leftCalfCm;
    }

    private static void ValidatePositiveWhenPresent(decimal? value, string measurementName)
    {
        if (value.HasValue && value.Value <= 0)
            throw new DomainException($"{measurementName} must be greater than zero when provided.");
    }
}
