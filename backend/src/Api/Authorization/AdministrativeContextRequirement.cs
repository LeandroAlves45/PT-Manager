using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization;

/// <summary>Exige role superuser e metadata administrativa no endpoint.</summary>
public sealed class AdministrativeContextRequirement : IAuthorizationRequirement;
