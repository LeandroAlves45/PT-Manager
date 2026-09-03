namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Classificação do desfecho da escrita do perfil.</summary>
public enum UpdateMyProfileStatus
{
    Updated,
    NotFound,
    DuplicateEmail,
    DuplicatePhone
}
