using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Supplements;

/// <summary>Suplemento global ou privado disponível no catálogo.</summary>
public sealed class Supplement
{
    public Guid Id { get; private set; }
    public Guid? OwnerTrainerId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string UnitOfMeasure { get; private set; } = null!;
    public string ServingSize { get; private set; } = null!;
    public string? Timing { get; private set; }
    public string? TrainerNotes { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Bloqueio da plataforma, separado da disponibilidade escolhida pelo dono.</summary>
    public PlatformEnforcementStatus PlatformEnforcementStatus { get; private set; } =
        PlatformEnforcementStatus.Allowed;
    public PlatformEnforcementReason? PlatformEnforcementReason { get; private set; }
    public DateTime? PlatformEnforcedAt { get; private set; }

    /// <summary>Nota livre do superuser que acompanha o motivo; o personal trainer vê-a no item.</summary>
    public string? PlatformEnforcementNote { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Supplement() { }

    /// <summary>Cria um suplemento ativo e preserva autoria separada da propriedade.</summary>
    public Supplement(
        Guid? ownerTrainerId,
        Guid createdByUserId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now
    )
    {
        if (ownerTrainerId.HasValue && ownerTrainerId.Value == Guid.Empty)
            throw new DomainException("Owner trainer ID cannot be empty.");
        if (createdByUserId == Guid.Empty)
            throw new DomainException("Creator user ID is required.");



        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        CreatedByUserId = createdByUserId;
        SetFields(name, description, unitOfMeasure, servingSize, timing, trainerNotes);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza os dados do catálogo sem alterar propriedade ou autoria.</summary>
    public void Update(
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string? timing,
        string? trainerNotes,
        DateTime now
    )
    {
        SetFields(name, description, unitOfMeasure, servingSize, timing, trainerNotes);
        UpdatedAt = now;
    }

    /// <summary>Arquiva o suplemento sem invalidar referências existentes.</summary>
    public void Archive(DateTime now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Volta a disponibilizar o suplemento para novas referências.</summary>

    /// <summary>Controla a disponibilidade sem eliminar referências históricas.</summary>
    public void Reactivate(DateTime now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>
    /// Bloqueia conteúdo privado com um motivo e uma nota livre opcional. O mesmo motivo e a
    /// mesma nota não criam uma nova transição.
    /// </summary>
    public bool Block(PlatformEnforcementReason reason, DateTime now, string? note = null)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (!OwnerTrainerId.HasValue)
            throw new DomainException("Only private catalog supplement can be blocked by platform enforcement.");

        var normalizedNote = PlatformEnforcementNotes.Normalize(note);
        if (PlatformEnforcementStatus == PlatformEnforcementStatus.Blocked &&
            PlatformEnforcementReason == reason &&
            PlatformEnforcementNote == normalizedNote)
            return false;

        PlatformEnforcementStatus = PlatformEnforcementStatus.Blocked;
        PlatformEnforcementReason = reason;
        PlatformEnforcementNote = normalizedNote;
        PlatformEnforcedAt = now;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Remove o bloqueio sem reativar o suplemento arquivado pelo personal trainer.</summary>
    public bool Unblock(DateTime now)
    {
        if (PlatformEnforcementStatus == PlatformEnforcementStatus.Allowed)
            return false;

        PlatformEnforcementStatus = PlatformEnforcementStatus.Allowed;
        PlatformEnforcementReason = null;
        PlatformEnforcementNote = null;
        PlatformEnforcedAt = null;
        UpdatedAt = now;
        return true;
    }

    /// <summary>Valida os parâmetros do suplemento.</summary>
    private void SetFields(
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string? timing,
        string? trainerNotes
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedUnit = SupplementUnits.NormalizeUnit(unitOfMeasure?.Trim() ?? string.Empty);
        var normalizedServingSize =
            SupplementUnits.NormalizeServingSize(servingSize?.Trim() ?? string.Empty);
        var normalizedTiming = NormalizeOptional(timing);

        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Supplement name must be between 1 and 255 characters.");
        if (normalizedUnit.Length is 0 or > 50)
            throw new DomainException("Unit of measure must contain between 1 and 50 characters.");
        if (normalizedServingSize.Length is 0 or > 100)
            throw new DomainException("Serving size must contain between 1 and 100 characters.");
        if (normalizedTiming?.Length > 255)
            throw new DomainException("Timing must contain at most 255 characters.");

        Name = normalizedName;
        Description = NormalizeOptional(description);
        UnitOfMeasure = normalizedUnit;
        ServingSize = normalizedServingSize;
        Timing = normalizedTiming;
        TrainerNotes = NormalizeOptional(trainerNotes);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
