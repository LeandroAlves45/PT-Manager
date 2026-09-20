namespace Application.Features.Clients.GetClientSummary;

/// <summary>
/// Janelas fixas do resumo. Ficam num único sítio porque mudam o
/// significado dos números mostrados ao personal trainer.
/// </summary>
public static class ClientSummaryWindows
{
    public const int AdherenceDays = 28;

    /// <summary>Dias considerados na variação de peso ("−1,4 kg em 8 semanas").</summary>
    public const int WeightChangeDays = 56;
}
