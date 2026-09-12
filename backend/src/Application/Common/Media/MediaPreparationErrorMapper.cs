using Application.Common.Abstractions;
using Application.Errors;

namespace Application.Common.Media;

/// <summary>
/// Converte um MediaPreparationStatus não bem-sucedido no erro estável da feature que a originou.
/// </summary>
public static class MediaPreparationErrorMapper
{
    /// <summary>Mapeia o estado terminal para erro.</summary>
    public static Error ToError(
        MediaPreparationOutcome outcome,
        string field,
        string codePrefix)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(codePrefix);

        return outcome.Status switch
        {
            MediaPreparationStatus.InvalidImage => Validation(
                field,
                $"{codePrefix}_{SuffixFor(outcome.ValidationFailure ?? ImageValidationFailure.NotDecodable)}",
                MessageFor(outcome.ValidationFailure ?? ImageValidationFailure.NotDecodable)),

            // Rejeição e revisão são deliberadamente indistinguíveis para o
            // cliente: revelar qual delas ocorreu daria a quem tenta evadir a
            // moderação um sinal para calibrar a tentativa seguinte.
            MediaPreparationStatus.Rejected or MediaPreparationStatus.ReviewRequired => Validation(
                field,
                $"{codePrefix}_content_not_allowed",
                "The image was not accepted. Please choose a different image."),

            MediaPreparationStatus.ModerationUnavailable => Error.Create(
                $"{codePrefix}_moderation_unavailable",
                ErrorCategory.ExternalDependency,
                "The image could not be reviewed right now. No changes were made."),

            MediaPreparationStatus.StorageDisabled or MediaPreparationStatus.StorageFailed =>
                Error.Create(
                    $"{codePrefix}_storage_unavailable",
                    ErrorCategory.ExternalDependency,
                    "The image could not be stored right now. No changes were made."),

            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                "A successful preparation has no error mapping."),
        };
    }

    private static Error Validation(
        string field,
        string code,
        string message) =>
            Error.Validation([
                new ValidationError(field, code, message)
            ]);

    private static string SuffixFor(ImageValidationFailure failure) => failure switch
    {
        ImageValidationFailure.Empty => "empty",
        ImageValidationFailure.TooLarge => "too_large",
        ImageValidationFailure.UnsupportedFormat => "unsupported_format",
        ImageValidationFailure.ContentTypeMismatch => "content_type_mismatch",
        ImageValidationFailure.NotDecodable => "not_decodable",
        ImageValidationFailure.DimensionsTooSmall => "dimensions_too_small",
        ImageValidationFailure.DimensionsTooLarge => "dimensions_too_large",
        ImageValidationFailure.PixelBudgetExceeded => "pixel_budget_exceeded",
        ImageValidationFailure.EncodingFailed => "encoding_failed",
        _ => "not_decodable"
    };

    private static string MessageFor(ImageValidationFailure failure) => failure switch
    {
        ImageValidationFailure.Empty => "The file is empty.",
        ImageValidationFailure.TooLarge => "The file exceeds the maximum allowed size.",
        ImageValidationFailure.UnsupportedFormat => "Only PNG, JPEG and WEBP images are accepted.",
        ImageValidationFailure.ContentTypeMismatch =>
            "The file content does not match the declared media type.",
        ImageValidationFailure.NotDecodable => "The file is not a readable image.",
        ImageValidationFailure.DimensionsTooSmall => "The image is too small.",
        ImageValidationFailure.DimensionsTooLarge => "The image is too large.",
        ImageValidationFailure.PixelBudgetExceeded => "The image has too many pixels.",
        ImageValidationFailure.EncodingFailed => "The image could not be processed.",
        _ => "The file is not a readable image."
    };
}
