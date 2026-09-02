using Api.Authorization;
using Api.Configuration;
using Api.Contracts.TrainerSettings;
using Application.Features.TrainerSettings.ChangeTimezone;
using Application.Features.TrainerSettings.GetTrainerSettings;
using Application.Features.TrainerSettings.RemoveLogo;
using Application.Features.TrainerSettings.ResetBrandingColors;
using Application.Features.TrainerSettings.UpdateBranding;
using Application.Features.TrainerSettings.UpdateContacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>Expõe as definições do personal trainer autenticado.</summary>
[Route("api/v1/trainer-settings")]
[Authorize(ApiPolicyNames.Trainer)]
[SensitiveResponse]
public sealed class TrainerSettingsController : ApiControllerBase
{
    /// <summary>Devolve as definições completas do tenant efetivo.</summary>
    [HttpGet]
    public Task<IActionResult> GetAsync(
        [FromServices] GetTrainerSettingsHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            TrainerSettingsResponse.From);

    /// <summary>Atualiza o branding e devolve as definições resultantes.</summary>
    [HttpPatch("branding")]
    public Task<IActionResult> UpdateBrandingAsync(
        [FromBody] UpdateBrandingRequest request,
        [FromServices] UpdateBrandingHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateBrandingCommand(
                    request.AppName,
                    request.PrimaryColor,
                    request.BodyColor
                ),
                cancellationToken),
            TrainerSettingsResponse.From);
    }

    /// <summary>Repõe as cores padrão do tema.</summary>
    [HttpPost("branding/reset-colors")]
    public Task<IActionResult> ResetBrandingColorsAsync(
        [FromServices] ResetBrandingColorsHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            TrainerSettingsResponse.From);

    /// <summary>Remove o logo atual e devolve as definições resultantes.</summary>
    [HttpDelete("logo")]
    public Task<IActionResult> RemoveLogoAsync(
        [FromServices] RemoveLogoHandler handler,
        CancellationToken cancellationToken) =>
        RespondAsync(
            handler.HandleAsync(cancellationToken),
            TrainerSettingsResponse.From);

    /// <summary>Atualiza os contactos opcionais.</summary>
    [HttpPatch("contacts")]
    public Task<IActionResult> UpdateContactsAsync(
        [FromBody] UpdateContactsRequest request,
        [FromServices] UpdateContactsHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new UpdateContactsCommand(
                    request.Phone,
                    request.Address,
                    request.City
                ),
                cancellationToken),
            TrainerSettingsResponse.From);
    }

    /// <summary>Altera o timezone IANA do personal trainer.</summary>
    [HttpPatch("timezone")]
    public Task<IActionResult> ChangeTimezoneAsync(
        [FromBody] ChangeTimezoneRequest request,
        [FromServices] ChangeTimezoneHandler handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RespondAsync(
            handler.HandleAsync(
                new ChangeTimezoneCommand(request.Timezone),
                cancellationToken),
            TrainerSettingsResponse.From);
    }
}
