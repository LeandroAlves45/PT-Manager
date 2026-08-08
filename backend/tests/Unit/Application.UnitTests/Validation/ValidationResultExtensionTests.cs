using Application.Errors;
using Application.Validation;
using FluentValidation.Results;
using Xunit;

namespace Application.UnitTests.Validation;

/// <summary>Verifica a conversão de FluentValidation para o contrato de Error.</summary>
public sealed class ValidationResultExtensionTests
{
    [Fact]
    public void InvalidResult_MapsEveryFailureInOriginalOrder()
    {
        var nameFailure = new List<ValidationFailure>
        {
            new ValidationFailure("Name", "Name is required.")
        };

        nameFailure[0].ErrorCode = "client_name_invalid";

        var phoneFailure = new List<ValidationFailure>
        {
            new ValidationFailure("Phone", "Phone is required.")
        };
        phoneFailure[0].ErrorCode = "client_phone_invalid";

        var validation = new ValidationResult(
            new List<ValidationFailure>(nameFailure.Concat(phoneFailure)));

        var error = validation.ToApplicationError();

        Assert.Equal(ErrorCategory.Validation, error.Category);
        Assert.Collection(
            error.ValidationErrors,
            detail =>
            {
                Assert.Equal("Name", detail.Field);
                Assert.Equal("Name is required.", detail.Message);
                Assert.Equal("client_name_invalid", detail.Code);
            },
            detail =>
            {
                Assert.Equal("Phone", detail.Field);
                Assert.Equal("Phone is required.", detail.Message);
                Assert.Equal("client_phone_invalid", detail.Code);
            }
        );
    }

    [Fact]
    public void ValidResult_ThrowsProgrammingError()
    {
        var validation = new ValidationResult();

        Assert.Throws<ArgumentException>(() => validation.ToApplicationError());
    }

    [Fact]
    public void NullResult_Throws()
    {
        ValidationResult? validation = null;

        Assert.Throws<ArgumentNullException>(() => ValidationResultExtension.ToApplicationError(validation!));
    }
}
