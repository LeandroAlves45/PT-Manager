namespace Application.Features.Clients.ListClients;

/// <summary>Solicita uma página determinística de clientes.</summary>
public sealed record ListClientsQuery(
    string? Search,
    ClientActivityFilter Activity = ClientActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50,
    bool WithoutTrainingPlan = false
);
