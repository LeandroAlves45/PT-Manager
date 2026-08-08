using Application.Errors;
using Application.Results;
using Xunit;

namespace Application.UnitTests.Results;

/// <summary>Verifica as invariantes públicas de Error, Result e Result de T.</summary>
public sealed class ResultAndErrorTests
{
    private static readonly Error SampleError = Error.Create(
        code: "sample_error",
        category: ErrorCategory.Conflict,
        description: "Safe description.");

    [Fact]
    public void Success_ExposesConsistentState()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_PreservesProvidedError()
    {
        var result = Result.Failure(SampleError);

        Assert.True(result.IsFailure);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Failure_NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void GenericSuccess_ExposesValues()
    {
        var result = Result<string>.Success("value");

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFailure_ValuesCannotBeRead()
    {
        var result = Result<string>.Failure(SampleError);

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        Assert.Same(SampleError, result.Error);
    }

    [Fact]
    public void Validation_CopiesSourceCollection()
    {
        var source = new List<ValidationError>
        {
            new ValidationError("Name", "required", "Name is required."),
            new ValidationError("Phone", "required", "Phone is required.")
        };

        var error = Error.Validation(source);
        source.Clear();

        Assert.Equal(ErrorCategory.Validation, error.Category);
        Assert.Equal(2, error.ValidationErrors.Count);
        Assert.Equal("validation_failed", error.Code);
    }

    [Fact]
    public void Create_CopiesMetadataDictionary()
    {
        var source = new Dictionary<string, object?>
        {
            { "resource_id", "original" }
        };

        var error = Error.Create(
            code: "sample_error",
            category: ErrorCategory.Conflict,
            description: "Safe description.",
            metadata: source);

        source["resource_id"] = "changed";

        Assert.Equal("original", error.Metadata["resource_id"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidCode_Throws(string? code)
    {
        Assert.Throws<ArgumentException>(() => Error.Create(
            code: code!,
            category: ErrorCategory.Conflict,
            description: "Safe description."));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidDescription_Throws(string? description)
    {
        Assert.Throws<ArgumentException>(() => Error.Create(
            code: "sample_error",
            category: ErrorCategory.Conflict,
            description: description!));
    }

    [Fact]
    public void Create_ValidationCategory_Throws()
    {
        Assert.Throws<ArgumentException>(() => Error.Create(
            code: "invalid",
            category: ErrorCategory.Validation,
            description: "Use the validation factory."));
    }

    [Fact]
    public void Validation_EmptyCollection_Throws()
    {
        Assert.Throws<ArgumentException>(() => Error.Validation(Array.Empty<ValidationError>()));
    }

    [Fact]
    public void Validation_NullCollection_Throws()
    {
        Assert.Throws<ArgumentException>(() => Error.Validation(null!));
    }

    [Fact]
    public void Validation_NullItem_Throws()
    {
        var errors = new List<ValidationError>
        {
            new ValidationError("Name", "required", "Name is required."),
            null!
        };

        Assert.Throws<ArgumentException>(() => Error.Validation(errors));
    }

    [Theory]
    [InlineData("", "code", "message")]
    [InlineData("field", "", "message")]
    [InlineData("field", "code", "")]
    public void Validation_BlankItemMember_Throws(
        string field,
        string code,
        string message)
    {
        var errors = new List<ValidationError>
        {
            new ValidationError(field, code, message)
        };

        Assert.Throws<ArgumentException>(() => Error.Validation(errors));
    }
}
