using Application.Features.Training.Exercises.CreateExercise;
using Application.Features.Training.Exercises.CreateGlobalExercise;
using Application.Features.Training.Exercises.UpdateExercise;
using Application.Features.Training.Exercises.UpdateGlobalExercise;
using FluentValidation.Results;
using Xunit;

namespace Application.UnitTests.Validation;

/// <summary>
/// A ligação de vídeo é aberta pelo cliente noutro separador: só YouTube e Vimeo em HTTPS. Os
/// quatro formulários de exercício (privado e global, criar e editar) aplicam a mesma regra.
/// </summary>
public sealed class ExternalVideoUrlRulesTests
{
    public static TheoryData<string?> Accepted => new()
    {
        null!,
        "",
        "   ",
        "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        "https://youtube.com/watch?v=dQw4w9WgXcQ",
        "https://m.youtube.com/watch?v=dQw4w9WgXcQ",
        "https://youtu.be/dQw4w9WgXcQ?t=30",
        "https://www.youtube.com/shorts/dQw4w9WgXcQ",
        "https://vimeo.com/76979871",
        "https://WWW.YOUTUBE.COM/watch?v=dQw4w9WgXcQ",
    };

    public static TheoryData<string, string> Rejected => new()
    {
        { "http://www.youtube.com/watch?v=x", "exercise_video_url_must_be_https" },
        { "javascript:alert(1)", "exercise_video_url_must_be_https" },
        { "www.youtube.com/watch?v=x", "exercise_video_url_must_be_https" },
        { "ftp://youtube.com/x", "exercise_video_url_must_be_https" },
        { "https://youtube.com.evil.example/watch?v=x", "exercise_video_url_host_not_allowed" },
        { "https://evil.example/youtube.com", "exercise_video_url_host_not_allowed" },
        { "https://www.youtube.com@evil.example/watch?v=x", "exercise_video_url_host_not_allowed" },
        { "https://user:pass@www.youtube.com/watch?v=x", "exercise_video_url_host_not_allowed" },
        { "https://www.youtube.com:8443/watch?v=x", "exercise_video_url_host_not_allowed" },
        { "https://notyoutube.com/watch?v=x", "exercise_video_url_host_not_allowed" },
        { "https://drive.google.com/file/d/x", "exercise_video_url_host_not_allowed" },
        { "https://youtu.be/" + new string('a', 490), "exercise_video_url_too_long" },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void AllFourValidators_AcceptYouTubeAndVimeoOrNoLink(string? videoUrl)
    {
        Assert.All(ValidateEverywhere(videoUrl), result => Assert.True(result.IsValid));
    }

    [Theory]
    [MemberData(nameof(Rejected))]
    public void AllFourValidators_RejectWithTheSameCode(string videoUrl, string errorCode)
    {
        Assert.All(ValidateEverywhere(videoUrl), result =>
        {
            var failure = Assert.Single(result.Errors);
            Assert.Equal("VideoUrl", failure.PropertyName);
            Assert.Equal(errorCode, failure.ErrorCode);
        });
    }

    private static IEnumerable<ValidationResult> ValidateEverywhere(string? videoUrl)
    {
        yield return new CreateExerciseCommandValidator().Validate(
            new CreateExerciseCommand("Agachamento", null, null, null, null, videoUrl));
        yield return new UpdateExerciseCommandValidator().Validate(
            new UpdateExerciseCommand(Guid.NewGuid(), "Agachamento", null, null, null, null, videoUrl));
        yield return new CreateGlobalExerciseCommandValidator().Validate(
            new CreateGlobalExerciseCommand("Agachamento", null, null, null, null, videoUrl));
        yield return new UpdateGlobalExerciseCommandValidator().Validate(
            new UpdateGlobalExerciseCommand(Guid.NewGuid(), "Agachamento", null, null, null, null, videoUrl));
    }
}
