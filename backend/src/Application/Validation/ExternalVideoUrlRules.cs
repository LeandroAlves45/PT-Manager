using Domain.ValueObjects;
using FluentValidation;

namespace Application.Validation;

/// <summary>
/// Validação da ligação de vídeo externa de um exercício (privado ou do catálogo global), com
/// códigos de erro por motivo. A regra vive no Domain (<see cref="ExternalVideoUrl"/>).
/// </summary>
internal static class ExternalVideoUrlRules
{
    /// <summary>
    /// Aceita null ou vazio (sem vídeo). Com valor: até 500 caracteres, HTTPS absoluto e host do
    /// YouTube ou do Vimeo.
    /// </summary>
    internal static IRuleBuilderOptions<T, string?> MustBeExternalVideoUrl<T>(
        this IRuleBuilderInitial<T, string?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .MaximumLength(ExternalVideoUrl.MaxLength)
            .WithErrorCode("exercise_video_url_too_long")
            .Must(value => string.IsNullOrWhiteSpace(value) || ExternalVideoUrl.IsAbsoluteHttps(value))
            .WithErrorCode("exercise_video_url_must_be_https")
            .Must(value => string.IsNullOrWhiteSpace(value) || ExternalVideoUrl.IsAllowed(value))
            .WithErrorCode("exercise_video_url_host_not_allowed")
            .WithMessage("Video link must be a YouTube or Vimeo address.");
}
