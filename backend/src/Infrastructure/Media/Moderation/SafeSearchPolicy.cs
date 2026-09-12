using Application.Common.Abstractions;

namespace Infrastructure.Media.Moderation;

/// <summary>Escala de probabilidade devolvida pelo SafeSearch, por ordem crescente.</summary>
internal enum SafeSearchLikelihood
{
    Unknown,
    VeryUnlikely,
    Unlikely,
    Possible,
    Likely,
    VeryLikely
}

/// <summary>Converte a anotação SafeSearch num veredicto de moderação.</summary>
internal static class SafeSearchPolicy
{
    internal static ImageModerationResult Evaluate(string? adult, string? violence, string? racy)
    {
        var adultLikelihood = Parse(adult);
        var violenceLikelihood = Parse(violence);
        var racyLikelihood = Parse(racy);

        if (adultLikelihood == SafeSearchLikelihood.Unknown ||
            violenceLikelihood == SafeSearchLikelihood.Unknown ||
            racyLikelihood == SafeSearchLikelihood.Unknown)
            return new ImageModerationResult(
                ImageModerationVerdict.Unavailable, "unknown_likelihood");

        if (adultLikelihood >= SafeSearchLikelihood.Likely)
            return new ImageModerationResult(ImageModerationVerdict.Rejected, "adult");

        if (violenceLikelihood >= SafeSearchLikelihood.Likely)
            return new ImageModerationResult(ImageModerationVerdict.Rejected, "violence");

        if (racyLikelihood == SafeSearchLikelihood.VeryLikely)
            return new ImageModerationResult(ImageModerationVerdict.Rejected, "racy");

        if (adultLikelihood == SafeSearchLikelihood.Possible)
            return new ImageModerationResult(ImageModerationVerdict.ReviewRequired, "adult");

        return new ImageModerationResult(ImageModerationVerdict.Approved);
    }

    internal static SafeSearchLikelihood Parse(string? value) => value switch
    {
        "VERY_UNLIKELY" => SafeSearchLikelihood.VeryUnlikely,
        "UNLIKELY" => SafeSearchLikelihood.Unlikely,
        "POSSIBLE" => SafeSearchLikelihood.Possible,
        "LIKELY" => SafeSearchLikelihood.Likely,
        "VERY_LIKELY" => SafeSearchLikelihood.VeryLikely,
        _ => SafeSearchLikelihood.Unknown
    };
}
