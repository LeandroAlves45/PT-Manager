namespace Application.Features.Nutrition.Foods.ImportGlobalFoods;

/// <summary>
/// Ficheiro CSV de alimentos globais. Com <see cref="DryRun"/> só valida e conta, sem gravar.
/// </summary>
public sealed record ImportGlobalFoodsCommand(Stream Content, bool DryRun);
