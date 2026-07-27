using Domain.Exceptions;
using System.Text.RegularExpressions;

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
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainerSettings() { }

    /// <summary>Cria as settings default de um personal trainer novo.</summary>
    public TrainerSettings(Guid trainerId, DateTime now)
    {
        Id = Guid.NewGuid();
        TrainerId = trainerId;
        AppName = "PT Manager";
        PrimaryColor = "#000000";
        BodyColor = "#FFFFFF";
        CreatedAt = now;
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
        if (string.IsNullOrWhiteSpace(appName))
            throw new DomainException("App name cannot be empty.");

        var normalizedAppName = appName.Trim();
        if (normalizedAppName.Length > 255)
            throw new DomainException("App name cannot exceed 255 characters.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(primaryColor, "^#[0-9A-Fa-f]{6}$"))
            throw new DomainException("Primary color invalid -> use hex code like #RRGGBB.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(bodyColor, "^#[0-9A-Fa-f]{6}$"))
            throw new DomainException("Body color invalid -> use hex code like #RRGGBB.");
        if (backgroundImageUrl != null && backgroundImageUrl.Length > 500)
            throw new DomainException("Background image URL cannot exceed 500 characters.");

        AppName = normalizedAppName;
        PrimaryColor = primaryColor;
        BodyColor = bodyColor;
        BackgroundImageUrl = backgroundImageUrl;
        UpdatedAt = now;
    }

    /// <summary>
    /// Substitui o logo, devolvendo o public_id antigo para o handler apagar
    /// o asset no Cloudinary.
    /// </summary>
    public string? ReplaceLogo(string logoUrl, string logoPublicId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
            throw new DomainException("Logo URL cannot be empty.");
        if (logoUrl.Length > 500)
            throw new DomainException("Logo URL cannot exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(logoPublicId))
            throw new DomainException("Logo public ID cannot be empty.");
        if (logoPublicId.Length > 500)
            throw new DomainException("Logo public ID cannot exceed 500 characters.");

        var previousPublicId = LogoPublicId;
        LogoUrl = logoUrl;
        LogoPublicId = logoPublicId;
        UpdatedAt = now;
        return previousPublicId;
    }

    /// <summary>Atualiza os dados de contacto.</summary>
    public void UpdateContacts(string? phone, string? address, string? city, DateTime now)
    {
        if (phone != null && phone.Length > 20)
            throw new DomainException("Phone cannot exceed 20 characters.");
        if (address != null && address.Length > 500)
            throw new DomainException("Address cannot exceed 500 characters.");
        if (city != null && city.Length > 255)
            throw new DomainException("City cannot exceed 255 characters.");

        Phone = phone;
        Address = address;
        City = city;
        UpdatedAt = now;
    }
}
