using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Identity;

/// <summary>
/// Utilizador da plataforma. Serve Personal Trainers, clientes e o superUser através
/// de uma coluna única <see cref="Role"/>  -> 3 roles fixas sem modelo N:N.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;

    /// <summary>Email normalizado (uppercase invariante) para lookups do Identity.</summary>
    public string NormalizedEmail { get; private set; } = null!;
    /// <summary>Hash da password, gerido por PasswordHasher&lt;User&gt; (nunca em claro).
    /// Nullable para validar se vai fazer INSERT com hash
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// Stamp de segurança do Identity, muda quando as credencias mudam (password, email, etc)
    /// </summary>
    public string SecurityStamp { get; private set; } = null!;

    /// <summary>Stamp de concorrência otimista do Identity.</summary>
    public string ConcurrencyStamp { get; private set; } = null!;

    public string? FullName { get; private set; }

    public string Role { get; private set; } = null!;

    public bool EmailConfirmed { get; private set; }

    /// <summary>Fim do lockout, se a conta estiver bloqueada por tentativas falhadas.</summary>
    public DateTime? LockoutEnd { get; private set; }

    /// <summary>Número de tentativas de login falhadas consecutivas.</summary>
    public int AccessFailedCount { get; private set; }
    /// <summary>Conta ativa? (desativação administrativa, distinta de soft delete).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Soft delete — o registo nunca é apagado fisicamente.</summary>
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private User() { } // EF Core

    /// <summary>
    /// Cria um novo utilizador com email por confirmar e conta ativa.
    /// </summary>
    public User(
        EmailAddress email,
        string role,
        string? fullName,
        DateTime now
    )
    {
        // GUARD: role tem que pertencer ao conjunto fechado
        string[] validRoles = { "superuser", "trainer", "client" };
        if (!validRoles.Contains(role))
            throw new DomainException("Invalid role");

        var normalizedFullName = fullName?.Trim();

        if (normalizedFullName != null && normalizedFullName.Length > 255)
            throw new DomainException("Full name cannot exceed 255 characters.");

        Id = Guid.NewGuid();
        Email = email.Value;
        NormalizedEmail = email.Normalized;
        PasswordHash = null;
        SecurityStamp = Guid.NewGuid().ToString();
        ConcurrencyStamp = Guid.NewGuid().ToString();
        Role = role;
        FullName = normalizedFullName;
        EmailConfirmed = false;
        AccessFailedCount = 0;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Marca o email como confirmado.</summary>
    public void ConfirmEmail(DateTime now)
    {
        EnsureNotDeleted();
        EmailConfirmed = true;
        UpdatedAt = now;
    }

    /// <summary>
    /// Regista uma tentativa de login falhada; o lockout em si é decidido
    /// pelo UserManager com base neste contador.
    /// </summary>
    public void RegisterFailedAccess(DateTime now)
    {
        EnsureNotDeleted();
        AccessFailedCount += 1;
        UpdatedAt = now;
    }

    /// <summary>Repõe o contador de tentativas falhadas (após login bem sucedido).</summary>
    public void ResetFailedAccess(DateTime now)
    {
        EnsureNotDeleted();
        AccessFailedCount = 0;
        LockoutEnd = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Define o hash produzido pelo UserManager/PasswordHasher.
    /// A entidade nunca recebe a password em claro.
    /// </summary>
    public void SetPasswordHash(string passwordHash, DateTime now)
    {
        EnsureNotDeleted();
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Password hash is required");
        if (passwordHash.Length > 255)
            throw new DomainException("Password hash cannot exceed 255 characters.");

        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    /// <summary>Define o stamp solicitado pelo IUserSecurityStampStore.</summary>
    public void SetSecurityStamp(string securityStamp, DateTime now)
    {
        EnsureNotDeleted();
        if (string.IsNullOrWhiteSpace(securityStamp))
            throw new DomainException("Security stamp is required");
        if (securityStamp.Length > 255)
            throw new DomainException("Security stamp cannot exceed 255 characters.");

        SecurityStamp = securityStamp;
        UpdatedAt = now;
    }

    /// <summary>Atualiza e normaliza o email através do IUserEmailStore.</summary>
    public void SetEmail(EmailAddress email, DateTime now)
    {
        EnsureNotDeleted();
        Email = email.Value;
        NormalizedEmail = email.Normalized;
        EmailConfirmed = false; // email mudou, tem que ser reconfirmado
        UpdatedAt = now;
    }

    /// <summary>Define o fim do lockout solicitado pelo UserManager.</summary>
    public void SetLockoutEnd(DateTime? lockoutEnd, DateTime now)
    {
        EnsureNotDeleted();
        LockoutEnd = lockoutEnd;
        UpdatedAt = now;
    }

    /// <summary>Roda o stamp usado pelo custom store para concorrência otimista.</summary>
    public void RotateConcurrencyStamp(DateTime now)
    {
        EnsureNotDeleted();
        ConcurrencyStamp = Guid.NewGuid().ToString();
        UpdatedAt = now;
    }

    /// <summary>Soft delete -> mantém o registo por integridade referencial e auditoria.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted user.");
    }
}
