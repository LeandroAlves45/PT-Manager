using Domain.Entities.Training;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Training;

public sealed class ExerciseTests
{
    private static readonly DateTime Now = new(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidValues_CreatesNormalizedActiveGlobalExercise()
    {
        var exercise = new Exercise(null, "  Bench press  ", null, " chest ", " barbell ",
            " intermediate ", "https://example.com/video", Now);

        Assert.Equal(("Bench press", "chest", true, null),
            (exercise.Name, exercise.MuscleGroups, exercise.IsActive, exercise.OwnerTrainerId));
    }

    [Fact]
    public void Constructor_WhenOwnerTrainerIdIsEmpty_ThrowsDomainException()
    {
        var action = () => new Exercise(Guid.Empty, "Bench press", null, null, null, null, null, Now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SetActive_ToSameValue_DoesNotChangeTimestamp()
    {
        var exercise = CreateExercise();

        exercise.SetActive(true, Now.AddMinutes(1));

        Assert.Equal(Now, exercise.UpdatedAt);
    }

    [Fact]
    public void SetActive_ToFalse_ArchivesExercise()
    {
        var exercise = CreateExercise();

        exercise.SetActive(false, Now.AddMinutes(1));

        Assert.False(exercise.IsActive);
    }

    [Fact]
    public void Update_AfterArchive_UpdatesEditableValues()
    {
        var exercise = CreateExercise();
        exercise.SetActive(false, Now.AddMinutes(1));

        exercise.Update("Incline bench press", null, "chest", "barbell", "advanced", null,
            Now.AddMinutes(2));

        Assert.Equal("Incline bench press", exercise.Name);
    }

    [Fact]
    public void Constructor_WithHttpVideoUrl_AcceptsValueBecauseHttpsIsApplicationInvariant()
    {
        var exercise = new Exercise(null, "Bench press", null, null, null, null,
            "http://example.com/video", Now);

        Assert.Equal("http://example.com/video", exercise.VideoUrl);
    }

    private static Exercise CreateExercise() =>
        new(null, "Bench press", null, null, null, null, null, Now);
}
