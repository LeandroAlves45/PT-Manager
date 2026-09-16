using Domain.Entities.Training;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Training;

/// <summary>
/// Prova as invariantes do vídeo gerido: identificador gerado pelo servidor,
/// transições válidas e recusa de estados que publicariam um vídeo não validado.
/// </summary>
public sealed class ExerciseVideoTests
{
    private static readonly DateTime Now = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.NewGuid();

    [Fact]
    public void Constructor_ForPrivateExercise_DerivesTheObjectKeyFromOwnerAndId()
    {
        var video = Pending(TrainerId);

        Assert.Equal(
            $"exercise-videos/trainers/{TrainerId:N}/{video.Id:N}",
            video.ObjectKey);
        Assert.Equal(ExerciseVideoStatus.Pending, video.Status);
    }

    [Fact]
    public void Constructor_ForGlobalExercise_UsesTheGlobalSpace()
    {
        var video = Pending(null);

        Assert.Equal($"exercise-videos/global/{video.Id:N}", video.ObjectKey);
    }

    [Theory]
    [InlineData("video/webm")]
    [InlineData("image/png")]
    [InlineData("VIDEO/MP4")]
    [InlineData("")]
    public void Constructor_RejectsContentTypesOutsideTheAllowlist(string contentType)
    {
        Assert.Throws<DomainException>(() => new ExerciseVideo(
            Guid.NewGuid(), TrainerId, contentType, 10, Guid.NewGuid(), Now.AddMinutes(15), Now));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveSizeAndPastExpiry()
    {
        Assert.Throws<DomainException>(() => new ExerciseVideo(
            Guid.NewGuid(), TrainerId, "video/mp4", 0, Guid.NewGuid(), Now.AddMinutes(15), Now));
        Assert.Throws<DomainException>(() => new ExerciseVideo(
            Guid.NewGuid(), TrainerId, "video/mp4", 10, Guid.NewGuid(), Now, Now));
    }

    [Fact]
    public void MarkUploaded_WithTheDeclaredSize_MovesToProcessing()
    {
        var video = Pending(TrainerId, size: 2048);

        video.MarkUploaded(2048, "etag-1", Now.AddMinutes(5));

        Assert.Equal(
            (ExerciseVideoStatus.Processing, 2048L, "etag-1", Now.AddMinutes(5)),
            (video.Status, video.StoredSizeBytes, video.StoredETag, video.ProcessingStartedAt));
    }

    [Fact]
    public void MarkUploaded_WhenTheStoredSizeDiffers_Throws()
    {
        var video = Pending(TrainerId, size: 2048);

        Assert.Throws<DomainException>(() => video.MarkUploaded(2049, "etag-1", Now));
        Assert.Equal(ExerciseVideoStatus.Pending, video.Status);
    }

    [Fact]
    public void MarkUploaded_AfterTheWindow_Throws()
    {
        var video = Pending(TrainerId, size: 2048);

        Assert.Throws<DomainException>(() =>
            video.MarkUploaded(2048, "etag-1", Now.AddMinutes(15).AddTicks(1)));
    }

    [Fact]
    public void MarkReady_FromPending_NeverPublishes()
    {
        var video = Pending(TrainerId);

        Assert.Throws<DomainException>(() => video.MarkReady(1000, 1280, 720, "avc1", null, Now));
        Assert.Null(video.ReadyAt);
    }

    [Fact]
    public void MarkReady_FromProcessing_StoresTheTechnicalMetadata()
    {
        var video = Processing(TrainerId);

        video.MarkReady(30_000, 1080, 1920, "avc1", "mp4a", Now.AddMinutes(2));

        Assert.Equal(
            (ExerciseVideoStatus.Ready, 30_000L, 1080, 1920, "avc1", "mp4a"),
            (video.Status, video.DurationMilliseconds, video.Width, video.Height, video.VideoCodec, video.AudioCodec));
    }

    [Theory]
    [InlineData("avc")]
    [InlineData("toolongcodec")]
    [InlineData("")]
    public void MarkReady_RejectsCodecsThatAreNotShortPrintableAscii(string codec)
    {
        var video = Processing(TrainerId);

        Assert.Throws<DomainException>(() => video.MarkReady(1000, 1280, 720, codec, null, Now));
    }

    [Fact]
    public void Reject_FromReady_IsForbidden()
    {
        var video = Processing(TrainerId);
        video.MarkReady(1000, 1280, 720, "avc1", null, Now);

        Assert.Throws<DomainException>(() => video.Reject("exercise_video_codec_unsupported", Now));
    }

    [Fact]
    public void Fail_FromPending_RecordsTheFailureCode()
    {
        var video = Pending(TrainerId);

        video.Fail("exercise_video_upload_abandoned", Now.AddHours(2));

        Assert.Equal(
            (ExerciseVideoStatus.Failed, "exercise_video_upload_abandoned"),
            (video.Status, video.FailureCode));
    }

    [Theory]
    [InlineData("Invalid Code")]
    [InlineData("code-with-dash")]
    [InlineData("")]
    public void Terminate_RejectsFailureCodesOutsideTheStableAlphabet(string failureCode)
    {
        var video = Pending(TrainerId);

        Assert.Throws<DomainException>(() => video.Reject(failureCode, Now));
    }

    [Fact]
    public void IsObjectKeyOwnedBy_AcceptsOnlyTheOwnerSpaceWithAServerIdentifier()
    {
        var video = Pending(TrainerId);
        var global = Pending(null);

        Assert.True(ExerciseVideo.IsObjectKeyOwnedBy(video.ObjectKey, TrainerId));
        Assert.True(ExerciseVideo.IsObjectKeyOwnedBy(global.ObjectKey, null));

        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(video.ObjectKey, Guid.NewGuid()));
        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(video.ObjectKey, null));
        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(global.ObjectKey, TrainerId));
        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(
            $"exercise-videos/trainers/{TrainerId:N}/../global/{video.Id:N}", TrainerId));
        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(
            $"exercise-videos/trainers/{TrainerId:N}/my-holiday.mp4", TrainerId));
        Assert.False(ExerciseVideo.IsObjectKeyOwnedBy(null, TrainerId));
    }

    private static ExerciseVideo Pending(Guid? owner, long size = 1024) =>
        new(Guid.NewGuid(), owner, "video/mp4", size, Guid.NewGuid(), Now.AddMinutes(15), Now);

    private static ExerciseVideo Processing(Guid? owner)
    {
        var video = Pending(owner, 1024);
        video.MarkUploaded(1024, "etag-1", Now.AddMinutes(1));
        return video;
    }
}
