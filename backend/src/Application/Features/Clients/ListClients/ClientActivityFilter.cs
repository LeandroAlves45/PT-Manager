namespace Application.Features.Clients.ListClients;

/// <summary>Seleciona o estado operacional incluído numa listagem de clientes.</summary>
public enum ClientActivityFilter
{
    /// <summary>Inclui apenas clientes ativos.</summary>
    Active,

    /// <summary>Inclui apenas clientes arquivados.</summary>
    Archived,

    /// <summary>Inclui ativos e arquivados, nunca soft deleted.</summary>
    All
}
