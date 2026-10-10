using Api.Contracts.Portal;
using Application.Features.ClientPortal.Dtos;

namespace Api.FunctionalTests.Contracts;

/// <summary>
/// O seed funcional só tem treino à terça, por isso os exercícios de
/// <c>my-workout/today</c> não são observáveis por HTTP nos outros dias. Este teste prova o
/// mapeamento do contrato de forma determinística.
/// </summary>
public sealed class PortalWorkoutContractsTests
{
    [Fact]
    public void WorkoutExercise_From_KeepsCatalogIdAndVideoFlagApartFromTheirNeighbours()
    {
        var prescriptionId = Guid.NewGuid();
        var catalogExerciseId = Guid.NewGuid();
        // Três booleanos seguidos num record posicional: só HasReadyVideo é verdadeiro, para
        // uma troca de posição com IsUnavailable ou IsCompleted ser visível.
        var dto = new MyWorkoutTodayDto.ExerciseDto(
            prescriptionId,
            catalogExerciseId,
            2,
            "Supino",
            IsUnavailable: false,
            HasReadyVideo: true,
            null,
            null,
            null,
            IsCompleted: false,
            []);

        var response = MyWorkoutExerciseResponse.From(dto);

        Assert.Equal(prescriptionId, response.Id);
        Assert.Equal(catalogExerciseId, response.ExerciseId);
        Assert.True(response.HasReadyVideo);
        Assert.False(response.IsUnavailable);
        Assert.False(response.IsCompleted);
    }
}
