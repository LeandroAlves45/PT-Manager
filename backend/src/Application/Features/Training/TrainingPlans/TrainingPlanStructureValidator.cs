using FluentValidation;

namespace Application.Features.Training.TrainingPlans;

/// <summary>Valida uma árvore final de treino e os respetivos IDs.</summary>
public sealed class TrainingPlanStructureValidator : AbstractValidator<TrainingPlanStructureInput>
{
    /// <summary>Cria regras de estrutura nova ou reconciliável.</summary>
    public TrainingPlanStructureValidator(bool requireNewIdentifiers)
    {
        RuleFor(input => input.Days)
            .NotNull()
            .WithErrorCode("training_days_required");

        RuleFor(input => input.Days)
            .Must(days => days.Select(day => (day.WeekNumber, day.DayOfWeek)).Distinct().Count()
                == days.Count)
            .WithErrorCode("training_day_duplicate");

        RuleFor(input => input.Days)
            .Must(days => UniqueNonNull(days.Select(day => day.Id)))
            .WithErrorCode("training_day_id_duplicate");

        RuleForEach(input => input.Days).ChildRules(day =>
        {
            day.RuleFor(value => value.Id)
                .Must(id => !requireNewIdentifiers || id is null)
                .WithErrorCode("training_new_structure_id_forbidden");
            day.RuleFor(value => value.Id)
                .Must(id => id is null || id != Guid.Empty)
                .WithErrorCode("training_day_id_invalid");
            day.RuleFor(value => value.DayOfWeek)
                .InclusiveBetween(0, 6)
                .WithErrorCode("training_day_of_week_invalid");
            day.RuleFor(value => value.WeekNumber)
                .InclusiveBetween(1, 52)
                .WithErrorCode("training_week_number_invalid");
            day.RuleFor(value => value.Exercises)
                .NotNull()
                .WithErrorCode("training_day_exercises_required")
                .Must(exercises => UniqueNonNull(exercises.Select(item => item.Id)))
                .WithErrorCode("training_day_exercise_id_duplicate");
            day.RuleFor(value => value.Exercises)
                .Must(exercises => exercises
                    .Where(item => item.ExerciseGroupId is null)
                    .Select(item => item.OrderNumber)
                    .Distinct()
                    .Count() == exercises.Count(item => item.ExerciseGroupId is null))
                .WithErrorCode("training_isolated_exercise_order_duplicate");
            day.RuleFor(value => value.Exercises)
                .Must(exercises => exercises
                    .Where(item => item.ExerciseGroupId.HasValue)
                    .GroupBy(item => item.ExerciseGroupId)
                    .All(group => group.Select(item => item.GroupPosition).Distinct().Count()
                        == group.Count()))
                    .WithErrorCode("training_group_position_duplicate");

            day.RuleForEach(value => value.Exercises).ChildRules(exercise =>
            {
                exercise.RuleFor(value => value.Id)
                    .Must(id => !requireNewIdentifiers || id is null)
                    .WithErrorCode("training_new_structure_id_forbidden");
                exercise.RuleFor(value => value.Id)
                    .Must(id => id is null || id != Guid.Empty)
                    .WithErrorCode("training_day_exercise_id_invalid");
                exercise.RuleFor(value => value.ExerciseId)
                    .NotEmpty()
                    .WithErrorCode("training_exercise_id_required");
                exercise.RuleFor(value => value.OrderNumber)
                    .GreaterThan(0)
                    .WithErrorCode("training_exercise_order_invalid");
                exercise.RuleFor(value => value)
                    .Must(value => value.ExerciseGroupId is null
                        ? value.GroupPosition is null
                        : value.ExerciseGroupId != Guid.Empty && value.GroupPosition > 0)
                    .WithErrorCode("training_exercise_group_invalid");
                exercise.RuleFor(value => value.Sets)
                    .NotNull()
                    .WithErrorCode("training_exercise_sets_required")
                    .Must(sets => UniqueNonNull(sets.Select(item => item.Id)))
                    .WithErrorCode("training_set_id_duplicate")
                    .Must(sets => sets.Select(item => item.SetNumber).Distinct().Count()
                        == sets.Count)
                    .WithErrorCode("training_set_number_duplicate");

                exercise.RuleForEach(value => value.Sets).ChildRules(set =>
                {
                    set.RuleFor(value => value.Id)
                        .Must(id => !requireNewIdentifiers || id is null)
                        .WithErrorCode("training_new_structure_id_forbidden");
                    set.RuleFor(value => value.Id)
                        .Must(id => id is null || id != Guid.Empty)
                        .WithErrorCode("training_set_id_invalid");
                    set.RuleFor(value => value.SetNumber)
                        .InclusiveBetween(1, 15)
                        .WithErrorCode("training_set_number_invalid");
                    set.RuleFor(value => value.PlannedReps)
                        .GreaterThan(0)
                        .When(value => value.PlannedReps.HasValue)
                        .WithErrorCode("training_planned_reps_invalid");
                    set.RuleFor(value => value.PlannedWeightKg)
                        .GreaterThanOrEqualTo(0)
                        .When(value => value.PlannedWeightKg.HasValue)
                        .WithErrorCode("training_planned_weight_invalid");
                    set.RuleFor(value => value.RestSecondsMin)
                        .GreaterThanOrEqualTo(0)
                        .When(value => value.RestSecondsMin.HasValue)
                        .WithErrorCode("training_rest_min_invalid");
                    set.RuleFor(value => value.RestSecondsMax)
                        .GreaterThanOrEqualTo(0)
                        .When(value => value.RestSecondsMax.HasValue)
                        .WithErrorCode("training_rest_max_invalid");
                    set.RuleFor(value => value)
                        .Must(value => !value.RestSecondsMin.HasValue ||
                            !value.RestSecondsMax.HasValue ||
                            value.RestSecondsMin <= value.RestSecondsMax)
                        .WithErrorCode("training_rest_range_invalid");
                });
            });
        });
    }

    private static bool UniqueNonNull(IEnumerable<Guid?> ids)
    {
        var materialized = ids.Where(id => id.HasValue).Select(id => id!.Value).ToArray();
        return materialized.Distinct().Count() == materialized.Length;
    }
}
