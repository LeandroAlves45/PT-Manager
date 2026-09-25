namespace Infrastructure.Seeding;

/// <summary>Configuração do seed de desenvolvimento.</summary>
public sealed class DevelopmentSeedOptions
{
    public const string SectionName = "DevelopmentSeed";

    /// <summary>
    /// Liga o seed. Falso por omissão: correr um seed sem intenção explícita numa base
    /// de dados errada é o tipo de acidente que não se desfaz.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Password das contas semeadas. Só existe em Development e só serve para entrar na
    /// aplicação local; nenhuma destas contas chega a outro ambiente.
    /// </summary>
    public string Password { get; set; } = string.Empty;
    public string SuperuserEmail { get; set; } = "admin@ptmanager.local";
    public string TrainerEmail { get; set; } = "trainer@ptmanager.local";
    public string ClientEmail { get; set; } = "cliente@ptmanager.local";

    public string SecondTrainerEmail { get; set; } = "trainer2@ptmanager.local";
}
