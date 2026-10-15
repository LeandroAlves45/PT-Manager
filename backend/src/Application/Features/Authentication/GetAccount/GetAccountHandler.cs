using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Authentication.Abstractions;
using Application.Features.Authentication.Dtos;
using Application.Results;

namespace Application.Features.Authentication.GetAccount;

/// <summary>Devolve os métodos de autenticação da conta autenticada.</summary>
public sealed class GetAccountHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly IAccountSecurityQueries _queries;

    /// <summary>Cria o handler com o contexto do utilizador e a leitura da conta.</summary>
    public GetAccountHandler(
        ITenantContext tenantContext,
        IAccountSecurityQueries queries)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>Lê a conta do utilizador autenticado; sem utilizador ou conta ativa dá 401.</summary>
    public async Task<Result<AccountSecurityDto>> HandleAsync(
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty)
            return Result<AccountSecurityDto>.Failure(CommonErrors.UnauthenticatedUser);

        var account = await _queries.GetAsync(_tenantContext.UserId.Value, cancellationToken);

        return account is null
            ? Result<AccountSecurityDto>.Failure(CommonErrors.UnauthenticatedUser)
            : Result<AccountSecurityDto>.Success(account);
    }
}
