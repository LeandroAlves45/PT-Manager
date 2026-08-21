using Domain.Exceptions;

namespace Domain.Entities.TrainerSettings;

/// <summary>
/// Configurações de branding e contacto de um personal trainer: nome da app,
/// logo (Cloudinary), cores e morada. Exatamente uma por personal trainer,
/// criada atomicamente no onboarding do personal trainer.
/// </summary>
public sealed class TrainerSettings
{
    public Guid Id { get; private set; }
    public Guid TrainerId { get; private set; }
    public string AppName { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public string? LogoPublicId { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? BodyColor { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public string? City { get; private set; }
    public string Timezone { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainerSettings() { }

    /// <summary>Cria as settings default de um personal trainer novo.</summary>
    public TrainerSettings(Guid trainerId, DateTime now)
    {
        if (trainerId == Guid.Empty)
            throw new DomainException("Trainer ID is required.");

        Id = Guid.NewGuid();
        TrainerId = trainerId;
        AppName = "PT Manager";
        LogoUrl = null;
        LogoPublicId = null;
        PrimaryColor = null;
        BodyColor = null;
        Timezone = "Europe/Lisbon";
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Atualiza o identificador IANA usado para apresentação e regras locais.
    /// Alterar para o timezone atual é um no-op idempotente. A validação IANA
    /// real (base de dados tz do sistema) pertence à Application; aqui só é
    /// garantida a forma sintática.
    /// </summary>
    public void ChangeTimezone(string timezone, DateTime now)
    {
        var normalized = timezone?.Trim() ?? string.Empty;
        if (!IsValidIanaShape(normalized))
            throw new DomainException("Timezone must be a valid IANA identifier");

        if (!string.Equals(Timezone, normalized, StringComparison.Ordinal))
            return;

        Timezone = normalized;
        UpdatedAt = now;
    }

    /// <summary>Atualiza o branding visual. Cores null repõem o tema padrão.</summary>
    public void UpdateBranding(
        string appName,
        string? primaryColor,
        string? bodyColor,
        DateTime now
    )
    {
        var normalizedAppName = appName?.Trim() ?? string.Empty;
        if (normalizedAppName.Length is < 2 or > 255)
            throw new DomainException("App name must contain between 2 and 50 characters.");
        if ((primaryColor is not null && !IsHexColor(primaryColor)) ||
            (bodyColor is not null && !IsHexColor(bodyColor)))
            throw new DomainException("Colors must use the #RRGGBB format.");

        AppName = normalizedAppName;
        PrimaryColor = primaryColor;
        BodyColor = bodyColor;
        UpdatedAt = now;
    }

    /// <summary>Repõe as duas cores do tema padrão. Idempotente.</summary>
    public void ResetColors(DateTime now)
    {
        if (PrimaryColor is null && BodyColor is null)
            return;

        PrimaryColor = null;
        BodyColor = null;
        UpdatedAt = now;
    }

    /// <summary>
    /// Substitui o logo, devolvendo o public_id antigo para o handler agendar
    /// a eliminação do asset anterior.
    /// </summary>
    public string? ReplaceLogo(string logoUrl, string logoPublicId, DateTime now)
    {
        var normalizedUrl = logoUrl?.Trim() ?? string.Empty;
        var normalizedPublicId = logoPublicId?.Trim() ?? string.Empty;
        if (normalizedUrl.Length is 0 or > 500 || normalizedPublicId.Length is 0 or > 500)
            throw new DomainException("Logo references must contain between 1 and 500 characters.");

        var previousPublicId = LogoPublicId;
        LogoUrl = normalizedUrl;
        LogoPublicId = normalizedPublicId;
        UpdatedAt = now;
        return previousPublicId;
    }

    /// <summary>
    /// Remove o logo atual, devolvendo o public_id antigo para o handler
    /// agendar a eliminação do asset. LogoUrl null é o contrato para o frontend
    /// voltar ao asset padrão. Sem logo personalizado, é um no-op idempotente.
    /// </summary>
    public string? RemoveLogo(DateTime now)
    {
        if (LogoPublicId is null && LogoUrl is null)
            return null;

        var previousPublicId = LogoPublicId;
        LogoUrl = null;
        LogoPublicId = null;
        UpdatedAt = now;
        return previousPublicId;
    }

    /// <summary>Atualiza os dados de contacto.</summary>
    public void UpdateContacts(string? phone, string? address, string? city, DateTime now)
    {
        if (phone is { Length: > 20 } || address is { Length: > 500 } || city is { Length: > 255 })
            throw new DomainException("Personal Trainer contact fields exceed their maximum length.");

        Phone = NormalizeOptional(phone);
        Address = NormalizeOptional(address);
        City = NormalizeOptional(city);
        UpdatedAt = now;
    }

    private static bool IsValidIanaShape(string value) =>
        value == "UTC" ||
        (value.Length is > 2 and <= 100 && value.Contains('/') &&
            value.All(c => char.IsLetterOrDigit(c) || c is '/' or '_' or '-' or '+'));

    private static bool IsHexColor(string value) =>
        value is { Length: 7 } && value[0] == '#' &&
        value[1..].All(Uri.IsHexDigit);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
