namespace Infrastructure.Seeding;

/// <summary>Configuração da criação do primeiro superuser em produção.</summary>
public sealed class SuperuserBootstrapOptions
{
    public const string SectionName = "SuperuserBootstrap";

    /// <summary>Comprimento mínimo da password: mais exigente do que o das contas normais (8).</summary>
    public const int MinimumPasswordLength = 12;

    /// <summary>
    /// Liga a criação. Falso por omissão: só se liga durante o arranque do primeiro deploy e
    /// desliga-se logo a seguir, para a password não ficar nas variáveis do serviço.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Email do superuser; fica com o email já confirmado.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Password inicial, com pelo menos <see cref="MinimumPasswordLength"/> caracteres.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Indica se as opções são utilizáveis. Desligado nunca falha: não há nada a validar.</summary>
    public bool IsValid() =>
        !Enabled
        || (!string.IsNullOrWhiteSpace(Email)
            && Password.Length >= MinimumPasswordLength);
}
