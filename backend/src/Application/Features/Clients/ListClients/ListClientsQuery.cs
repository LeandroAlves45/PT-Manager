namespace Application.Features.Clients.ListClients;

/// <summary>Solicita uma página determinística de clientes.</summary>
/// <param name="Search">Pesquisa opcional por nome, email ou telefone.</param>
/// <param name="Activity">Estado pretendido; Active por omissão.</param>
/// <param name="PageNumber">Página baseada em um.</param>
/// <param name="PageSize">Itens entre um e cem.</param>
public sealed record ListClientsQuery(
    string? Search,
    ClientActivityFilter Activity = ClientActivityFilter.Active,
    int PageNumber = 1,
    int PageSize = 50
);
