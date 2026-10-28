using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Clients;

/// <summary>
/// Ficha de um cliente pertencente a um personal trainer.
/// A ficha existe independentemente de o cliente já ter uma conta de acesso.
/// </summary>
public sealed class Client
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    /// <summary>Conta de utilizador associada (role "client").</summary>
    public Guid? UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? ContactEmail { get; private set; }
    public string? NormalizedContactEmail { get; private set; }
    public string Phone { get; private set; } = null!;
    public BirthDate BirthDate { get; private set; } = null!;
    public BiologicalSex Sex { get; private set; } = null!;
    public string? Objective { get; private set; }
    public string? Notes { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? AvatarPublicId { get; private set; }
    /// <summary>
    /// Plano alimentar que o cliente escolheu seguir no portal. Fica guardado na conta até ele
    /// escolher outro; arquivar o plano limpa a escolha. É escrito só pelos stores, com uma
    /// atualização direta que confirma o plano na mesma instrução.
    /// </summary>
    public Guid? SelectedMealPlanId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Client() { } // EF Core

    /// <summary>
    /// Cria um cliente ativo para o personal trainer indicado.
    /// </summary>
    public Client(
        Guid ownerTrainerId,
        string name,
        string? contactEmail,
        string phone,
        BirthDate birthDate,
        BiologicalSex sex,
        string? objective,
        string? notes,
        string? emergencyContactName,
        string? emergencyContactPhone,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty)
            throw new DomainException("Owner trainer ID is required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        UserId = null;
        SetProfile(name, contactEmail, phone, birthDate, sex, objective, notes,
            emergencyContactName, emergencyContactPhone, DateOnly.FromDateTime(now));
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza os dados permanentes da ficha, sem alterar o tenant ou a conta.</summary>
    public void UpdateProfile(
        string name,
        string? contactEmail,
        string phone,
        BirthDate birthDate,
        BiologicalSex sex,
        string? objective,
        string? notes,
        string? emergencyContactName,
        string? emergencyContactPhone,
        DateTime now)
    {
        EnsureNotDeleted();
        SetProfile(name, contactEmail, phone, birthDate, sex, objective, notes,
            emergencyContactName, emergencyContactPhone, DateOnly.FromDateTime(now));
        UpdatedAt = now;
    }

    /// <summary>
    /// Associa a conta criada durante a aceitação do convite.
    /// A associação é de uso único para evitar trocar silencionsamente a identidade do cliente.
    /// </summary>
    public void AttachUser(Guid userId, DateTime now)
    {
        EnsureNotDeleted();
        if (userId == Guid.Empty)
            throw new DomainException("User ID is required.");
        if (UserId.HasValue)
            throw new DomainException("Client already has an associated user.");

        UserId = userId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Substitui o avatar apresentado no portal do cliente e devolve o
    /// identificador do asset anterior, para que o chamador agende a sua
    /// eliminação por outbox depois do commit.
    /// </summary>
    public string? ReplaceAvatar(string avatarUrl, string avatarPublicId, DateTime now)
    {
        EnsureNotDeleted();

        var normalizedUrl = avatarUrl?.Trim();
        var normalizedPublicId = avatarPublicId?.Trim();

        if (normalizedUrl is not { Length: > 0 and <= 500 } ||
            normalizedPublicId is not { Length: > 0 and <= 500 })
            throw new DomainException(
                "Avatar references must contain between 1 and 500 characters.");

        var previousPublicId = AvatarPublicId;

        AvatarUrl = normalizedUrl;
        AvatarPublicId = normalizedPublicId;
        UpdatedAt = now;

        return previousPublicId;
    }

    /// <summary>
    /// Remove o avatar. É idempotente: sem avatar não escreve nem altera
    /// UpdateAt, para que um retry não produza histórico falso.
    /// </summary>
    public string? RemoveAvatar(DateTime now)
    {
        EnsureNotDeleted();

        if (AvatarUrl is null && AvatarPublicId is null)
            return null;

        var previousPublicId = AvatarPublicId;
        AvatarUrl = null;
        AvatarPublicId = null;
        UpdatedAt = now;

        return previousPublicId;
    }

    /// <summary>Desativa o cliente (arquivado) sem o apagar.</summary>
    public void Deactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Reativa o cliente arquivado.</summary>
    public void Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Nome mostrado ao personal trainer depois de o cliente eliminar a conta.</summary>
    public const string AnonymizedName = "Cliente eliminado";

    /// <summary>
    /// O cliente eliminou a conta: apaga o que o identifica e arquiva a ficha, mantendo o
    /// histórico (planos, avaliações, check-ins, treinos) no tenant do personal trainer sem
    /// identificar a pessoa. Devolve o avatar anterior, para o chamador agendar a eliminação
    /// do ficheiro depois do commit.
    /// </summary>
    /// <remarks>
    /// O telefone é obrigatório e único por tenant, por isso passa a um marcador derivado do
    /// id. A data de nascimento fica só com o ano (1 de janeiro): o personal trainer mantém a
    /// faixa etária do histórico sem a data que identifica a pessoa. A conta associada
    /// continua ligada para que outra conta não possa ser ligada a este histórico.
    /// </remarks>
    public string? Anonymize(DateTime now)
    {
        EnsureNotDeleted();

        var previousAvatarPublicId = AvatarPublicId;

        Name = AnonymizedName;
        ContactEmail = null;
        NormalizedContactEmail = null;
        Phone = AnonymizedPhoneFor(Id);
        BirthDate = AnonymizedBirthDate(BirthDate);
        Objective = null;
        Notes = null;
        EmergencyContactName = null;
        EmergencyContactPhone = null;
        AvatarUrl = null;
        AvatarPublicId = null;
        SelectedMealPlanId = null;
        IsActive = false;
        UpdatedAt = now;

        return previousAvatarPublicId;
    }

    /// <summary>Telefone de uma ficha anonimizada: único por ficha e com 32 caracteres.</summary>
    public static string AnonymizedPhoneFor(Guid clientId) => $"removido-{clientId:N}"[..32];

    /// <summary>Data de nascimento de uma ficha anonimizada: só o ano, a 1 de janeiro.</summary>
    public static BirthDate AnonymizedBirthDate(BirthDate birthDate)
    {
        ArgumentNullException.ThrowIfNull(birthDate);
        return BirthDate.FromPersisted(new DateOnly(birthDate.Value.Year, 1, 1));
    }

    /// <summary>Soft delete -> planos e histórico continuam consultáveis por integridade.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Valida os parâmetros do cliente.</summary>
    private void SetProfile(
        string name,
        string? contactEmail,
        string phone,
        BirthDate birthDate,
        BiologicalSex sex,
        string? objective,
        string? notes,
        string? emergencyContactName,
        string? emergencyContactPhone,
        DateOnly today
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedPhone = phone?.Trim() ?? string.Empty;
        var normalizedObjective = NormalizeOptional(objective);
        var normalizedEmergencyName = NormalizeOptional(emergencyContactName);
        var normalizedEmergencyPhone = NormalizeOptional(emergencyContactPhone);

        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Client name must contain between 1 and 255 characters.");
        if (normalizedPhone.Length is 0 or > 32)
            throw new DomainException("Client phone must contain between 1 and 32 characters.");
        if (birthDate is null)
            throw new DomainException("Birth date is required.");
        if (birthDate.Value > today)
            throw new DomainException("Birth date cannot be in the future.");
        if (sex is null)
            throw new DomainException("Biological sex is required.");
        if (normalizedObjective is { Length: > 255 })
            throw new DomainException("Objective cannot exceed 255 characters.");
        if (normalizedEmergencyName is { Length: > 255 })
            throw new DomainException("Emergency contact name cannot exceed 255 characters.");
        if (normalizedEmergencyPhone is { Length: > 32 })
            throw new DomainException("Emergency contact phone cannot exceed 32 characters.");

        if (string.IsNullOrWhiteSpace(contactEmail))
        {
            ContactEmail = null;
            NormalizedContactEmail = null;
        }
        else
        {
            var email = new EmailAddress(contactEmail);
            ContactEmail = email.Value;
            NormalizedContactEmail = email.Normalized;
        }

        Name = normalizedName;
        Phone = normalizedPhone;
        BirthDate = birthDate;
        Sex = sex;
        Objective = normalizedObjective;
        Notes = NormalizeOptional(notes);
        EmergencyContactName = normalizedEmergencyName;
        EmergencyContactPhone = normalizedEmergencyPhone;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted client.");
    }
}
