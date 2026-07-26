namespace Domain.Entities.Clients;

/// <summary>
/// Cliente de um Personal Trainer. Liga a um User (conta de acesso) ao personal trainer
/// dono do tenantm, com o perfil de treino (objetivo, bio, avatar).
/// </summary>
/// <remarks>
/// Multi-tenant: é obrigatório e imutável após a criação.
/// Um cliente não muda de Personal Trainer, se isso acontecer, é um novo cliente (novo registo).
/// </remarks>
public class Client
{
    public Guid Id { get; private set; }
    /// <summary>Personal trainer dono do tenant (chave tenant, Global Query Filter).</summary>
    public Guid OwnerTrainerId { get; private set; }
    /// <summary>Conta de utilizador associada (role "client").</summary>
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Objective { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
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
        Guid userId,
        string name,
        DateTime now
    )
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Client name is required");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        UserId = userId;
        Name = name.Trim();
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza o perfil apresentado (nome, objetivo, bio).</summary>
    public void UpdateProfile(string name, string? objective, string? bio, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Client name is required");

        Name = name.Trim();
        Objective = objective?.Trim();
        Bio = bio?.Trim();
        UpdatedAt = now;
    }

    /// <summary>Desativa o cliente (arquivado) sem o apagar.</summary>
    public void Deactivate(DateTime now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Reativa o cliente arquivado.</summary>
    public void Reactivate(DateTime now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>Soft delete -> planos e histórico continuam consultáveis por integridade.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }
}
