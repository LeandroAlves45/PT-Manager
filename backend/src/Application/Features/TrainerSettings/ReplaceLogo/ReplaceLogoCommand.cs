using Application.Common.Abstractions;

namespace Application.Features.TrainerSettings.ReplaceLogo;

/// <summary>Novo logo a substituir o atual.</summary>
public sealed record ReplaceLogoCommand(MediaUpload Logo);
