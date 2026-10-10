using Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;
using FluentValidation.Results;

namespace Application.UnitTests.Features.Training;

/// <summary>
/// Os quatro validadores de séries (trainer e cliente, registar e corrigir) partilham o
/// máximo de carga do Domain: acima dele a API devolve 400 e nunca chega a um overflow (500)
/// da coluna numeric(10,2).
/// </summary>
public sealed class ExerciseSetLogWeightValidatorsTests
{
    private static readonly DateTimeOffset PerformedAt = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, true)]
    [InlineData(1000, true)]
    [InlineData(1000.01, false)]
    [InlineData(123456789012, false)]
    public void EveryWeightValidator_AcceptsUpToTheMaximumOnly(double weight, bool valid)
    {
        var weightKg = (decimal)weight;

        ValidationResult[] results =
        [
            new RecordExerciseSetLogCommandValidator().Validate(
                new RecordExerciseSetLogCommand(Guid.NewGuid(), 1, weightKg, 8, null, PerformedAt)),
            new CorrectExerciseSetLogCommandValidator().Validate(
                new CorrectExerciseSetLogCommand(Guid.NewGuid(), weightKg, 8, null, PerformedAt)),
            new RecordMyExerciseSetLogCommandValidator().Validate(
                new RecordMyExerciseSetLogCommand(Guid.NewGuid(), 1, weightKg, 8, null, null)),
            new CorrectMyExerciseSetLogCommandValidator().Validate(
                new CorrectMyExerciseSetLogCommand(Guid.NewGuid(), weightKg, 8, null, null))
        ];

        Assert.All(results, result =>
        {
            Assert.Equal(valid, result.IsValid);
            if (!valid)
                Assert.Equal("training_weight_invalid", Assert.Single(result.Errors).ErrorCode);
        });
    }
}
