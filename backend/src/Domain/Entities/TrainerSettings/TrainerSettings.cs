using Domain.Exceptions;

namespace Domain.Entities.TrainerSettings;

/// <summary>
/// Configurações de branding e contato de um personal trainer: nome da app, logo (Cloudinary),
/// cores e morada. Exatamente uma por personal trainer.
/// </summary>
public class TrainerSettings
{
    public Guid Id { get; private set; }
    public Guid TrainerId { get; private set; }
    /// <summary>Nome da app apresentado ao cliente. Default 'PT Manager'.</summary>
    public string AppName { get; private set; } = null!;
    public string? LogoUrl { get; private set; }
    public string? LogoPublicId { get; private set; }
    public string PrimaryColor { get; private set; } = null!;
    public string BodyColor { get; private set; } = null!;
    public string? BackgroundImageUrl { get; private set; }
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
        PrimaryColor = "#000000";
        BodyColor = "#FFFFFF";
        Timezone = "Europe/Lisbon";
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza o identificador IANA usado para aprensetação e regras locais.</summary>
    public void ChangeTimezone(string timezone, DateTime now)
    {
        var normalized = timezone?.Trim() ?? string.Empty;
        if (!IsValidIanaShape(normalized))
            throw new DomainException("Timezone must be a valid IANA identifier");

        Timezone = normalized;
        UpdatedAt = now;
    }

    /// <summary>Atualiza o branding visual.</summary>
    public void UpdateBranding(
        string appName,
        string primaryColor,
        string bodyColor,
        string? backgroundImageUrl,
        DateTime now
    )
    {
        var normalizedAppName = appName?.Trim() ?? string.Empty;
        if (normalizedAppName.Length is 0 or > 255)
            throw new DomainException("App name must contain between 1 and 255 characters.");
        if (!IsHexColor(primaryColor) || !IsHexColor(bodyColor))
            throw new DomainException("Colors must use the #RRGGBB format.");
        if (backgroundImageUrl is { Length: > 500 })
            throw new DomainException("Background image URL cannot exceed 500 characters.");

        AppName = normalizedAppName;
        PrimaryColor = primaryColor;
        BodyColor = bodyColor;
        BackgroundImageUrl = NormalizeOptional(backgroundImageUrl);
        UpdatedAt = now;
    }

    /// <summary>
    /// Substitui o logo, devolvendo o public_id antigo para o handler apagar
    /// o asset no Cloudinary.
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

    /// <summary>Atualiza os dados de contacto.</summary>
    public void UpdateContacts(string? phone, string? address, string? city, DateTime now)
    {
        if (phone is { Length: > 32 } || address is { Length: > 500 } || city is { Length: > 255 })
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
