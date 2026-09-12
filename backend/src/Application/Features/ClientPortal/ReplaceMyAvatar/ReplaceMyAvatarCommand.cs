using Application.Common.Abstractions;

namespace Application.Features.ClientPortal.ReplaceMyAvatar;

/// <summary>Nova fotografia de perfil do próprio cliente.</summary>
public sealed record ReplaceMyAvatarCommand(MediaUpload Avatar);
