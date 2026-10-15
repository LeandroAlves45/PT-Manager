using Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace Api.Configuration;

/// <summary>
/// Recusa <c>DevelopmentEmail:LogLinks</c> fora de Development: o link do log é uma
/// credencial de conta, e escrevê-la em log de produção anularia a confirmação de email.
/// Vive na Api porque é o composition root que conhece o ambiente.
/// </summary>
internal sealed class DevelopmentEmailOptionsValidator : IValidateOptions<DevelopmentEmailOptions>
{
    private readonly IHostEnvironment _environment;

    public DevelopmentEmailOptionsValidator(IHostEnvironment environment) =>
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));

    public ValidateOptionsResult Validate(string? name, DevelopmentEmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.LogLinks && !_environment.IsDevelopment()
            ? ValidateOptionsResult.Fail(
                "Configuration 'DevelopmentEmail:LogLinks' is only allowed in the Development environment.")
            : ValidateOptionsResult.Success;
    }
}
