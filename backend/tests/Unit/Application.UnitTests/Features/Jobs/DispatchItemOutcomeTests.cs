using Application.Features.Jobs.Dispatching;

namespace Application.UnitTests.Features.Jobs;

/// <summary>
/// Verifica as invariantes de <see cref="DispatchItemOutcome"/>.
///
/// O dispatcher persiste directamente o
/// estado devolvido por este tipo. Se uma factory aceitasse um código vazio ou
/// arbitrariamente longo, esse valor chegaria à coluna de erro do job sem mais
/// nenhuma validação pelo caminho.
/// </summary>
public sealed class DispatchItemOutcomeTests
{
    [Fact]
    public void Succeeded_HasNoFailureCode()
    {
        var outcome = DispatchItemOutcome.Succeeded();

        Assert.Equal(DispatchItemOutcomeKind.Succeeded, outcome.Kind);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public void LeaseLost_HasNoFailureCode()
    {
        var outcome = DispatchItemOutcome.LeaseLost();

        Assert.Equal(DispatchItemOutcomeKind.LeaseLost, outcome.Kind);
        Assert.Null(outcome.FailureCode);
    }

    [Fact]
    public void TransientFailure_PreservesKindAndCode()
    {
        var outcome = DispatchItemOutcome.TransientFailure("resend_http_503");

        Assert.Equal(DispatchItemOutcomeKind.TransientFailure, outcome.Kind);
        Assert.Equal("resend_http_503", outcome.FailureCode);
    }

    [Fact]
    public void PermanentFailure_PreservesKindAndCode()
    {
        var outcome = DispatchItemOutcome.PermanentFailure("notification_not_found");

        Assert.Equal(DispatchItemOutcomeKind.PermanentFailure, outcome.Kind);
        Assert.Equal("notification_not_found", outcome.FailureCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Failures_RejectBlankCode(string failureCode)
    {
        Assert.Throws<ArgumentException>(
            () => DispatchItemOutcome.TransientFailure(failureCode));
        Assert.Throws<ArgumentException>(
            () => DispatchItemOutcome.PermanentFailure(failureCode));
    }

    [Fact]
    public void Failures_RejectNullCode()
    {
        Assert.Throws<ArgumentException>(
            () => DispatchItemOutcome.TransientFailure(null!));
        Assert.Throws<ArgumentException>(
            () => DispatchItemOutcome.PermanentFailure(null!));
    }

    [Fact]
    public void Failures_AcceptCodeAtMaximumLength()
    {
        var code = new string('a', 100);

        Assert.Equal(code, DispatchItemOutcome.TransientFailure(code).FailureCode);
        Assert.Equal(code, DispatchItemOutcome.PermanentFailure(code).FailureCode);
    }

    [Fact]
    public void Failures_RejectCodeAboveMaximumLength()
    {
        var code = new string('a', 101);

        Assert.Throws<ArgumentException>(() => DispatchItemOutcome.TransientFailure(code));
        Assert.Throws<ArgumentException>(() => DispatchItemOutcome.PermanentFailure(code));
    }
}
