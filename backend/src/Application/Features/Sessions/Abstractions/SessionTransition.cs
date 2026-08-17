namespace Application.Features.Sessions.Abstractions;

/// <summary>Transições de estado persistidas atomicamente pelo store.</summary>
public enum SessionTransition
{
    Complete,
    CancelByClient,
    CancelByTrainer,
    MarkNoShow,
    Restore
}
