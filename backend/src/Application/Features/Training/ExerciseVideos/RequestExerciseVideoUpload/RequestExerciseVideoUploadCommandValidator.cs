using FluentValidation;

namespace Application.Features.Training.ExerciseVideos.RequestExerciseVideoUpload;

/// <summary>
/// Valida o que é conhecido sem I/O. O tamanho e o tipo declarados são só a
/// autorização pedida: a finalização confirma-os no fornecedor.
/// </summary>
public sealed class RequestExerciseVideoUploadCommandValidator
    : AbstractValidator<RequestExerciseVideoUploadCommand>
{
    public RequestExerciseVideoUploadCommandValidator()
    {
        RuleFor(command => command.Catalog)
            .IsInEnum()
            .WithErrorCode("exercise_video_catalog_invalid");

        RuleFor(command => command.ExerciseId)
            .NotEmpty()
            .WithErrorCode("exercise_id_required");

        RuleFor(command => command.ContentType)
            .Must(contentType => contentType is not null &&
                ExerciseVideoPolicy.AcceptedContentTypes.Contains(contentType))
            .WithErrorCode("exercise_video_content_type_unsupported");

        RuleFor(command => command.SizeBytes)
            .InclusiveBetween(1, ExerciseVideoPolicy.MaxSizeBytes)
            .WithErrorCode("exercise_video_size_invalid");
    }
}
