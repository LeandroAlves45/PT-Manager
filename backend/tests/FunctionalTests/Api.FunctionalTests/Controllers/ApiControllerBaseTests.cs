using System.Reflection;
using Api.Controllers;
using Application.Errors;
using Application.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.FunctionalTests.Controllers;

public sealed class ApiControllerBaseTests
{
    [Fact]
    public async Task RespondAsync_WhenSuccess_ReturnsNoContent()
    {
        var controller = CreateController();

        var response = await controller.RespondVoidAsync(Task.FromResult(Result.Success()));

        Assert.IsType<NoContentResult>(response);
    }

    [Fact]
    public async Task RespondAsync_WhenSuccessWithValue_ReturnsOkWithProjectedBody()
    {
        var controller = CreateController();

        var response = await controller.RespondValueAsync(
            Task.FromResult(Result<int>.Success(42)),
            value => new DoubledResponse(value * 2));

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<DoubledResponse>(ok.Value);
        Assert.Equal(84, body.doubled);
    }

    [Fact]
    public async Task RespondCreatedAsync_WhenSuccess_ReturnsCreatedWithLocationHeader()
    {
        var controller = CreateController();
        var resourceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var response = await controller.RespondCreatedValueAsync(
            Task.FromResult(Result<Guid>.Success(resourceId)),
            value => new IdResponse(value),
            value => $"/api/v1/resources/{value}");

        var created = Assert.IsType<CreatedResult>(response);
        Assert.Equal($"/api/v1/resources/{resourceId}", created.Location);
        var body = Assert.IsType<IdResponse>(created.Value);
        Assert.Equal(resourceId, body.id);
    }

    [Fact]
    public async Task RespondOptionalAsync_WhenValueIsNull_ReturnsNotFoundProblem()
    {
        var controller = CreateController();

        var response = await controller.RespondOptionalValueAsync(
            Task.FromResult(Result<string?>.Success(null)),
            value => new { name = value });

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("resource_not_found", problem.Title);
    }

    [Fact]
    public async Task RespondOptionalAsync_WhenValueExists_ReturnsOkWithProjectedBody()
    {
        var controller = CreateController();

        var response = await controller.RespondOptionalValueAsync(
            Task.FromResult(Result<string?>.Success("alpha")),
            value => new NameResponse(value));

        var ok = Assert.IsType<OkObjectResult>(response);
        var body = Assert.IsType<NameResponse>(ok.Value);
        Assert.Equal("alpha", body.name);
    }

    [Theory]
    [InlineData(ErrorCategory.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorCategory.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorCategory.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorCategory.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorCategory.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorCategory.PaymentRequired, StatusCodes.Status402PaymentRequired)]
    [InlineData(ErrorCategory.ExternalDependency, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorCategory.Internal, StatusCodes.Status500InternalServerError)]
    public async Task RespondAsync_MapsErrorCategoryToExpectedStatus(
        ErrorCategory category,
        int expectedStatus)
    {
        var controller = CreateController();
        var error = category == ErrorCategory.Validation
            ? Error.Validation([new ValidationError("field", "invalid", "Invalid value.")])
            : Error.Create("test_error", category, "Test error.");

        var response = await controller.RespondVoidAsync(Task.FromResult(Result.Failure(error)));

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
    }

    [Fact]
    public void ApiControllerBase_IsTheSingleRespondEntryPointForBusinessControllers()
    {
        var businessControllers = typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                typeof(ControllerBase).IsAssignableFrom(type) &&
                type != typeof(AuthController) &&
                type != typeof(AdminContentModerationController))
            .ToArray();

        Assert.All(businessControllers, controllerType =>
            Assert.True(typeof(ApiControllerBase).IsAssignableFrom(controllerType)));

        // Herdar da base não chega: um controller podia herdar e continuar a declarar
        // o seu próprio Respond, que é exatamente a duplicação que o gate proíbe.
        var declaredRespondMethods = businessControllers
            .SelectMany(controllerType => controllerType.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly))
            .Where(method => method.Name.StartsWith("Respond", StringComparison.Ordinal))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(declaredRespondMethods);
    }

    private static TestApiController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-4a";

        return new TestApiController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private sealed class TestApiController : ApiControllerBase
    {
        public Task<IActionResult> RespondVoidAsync(Task<Result> operation) => RespondAsync(operation);

        public Task<IActionResult> RespondValueAsync<TValue, TResponse>(
            Task<Result<TValue>> operation,
            Func<TValue, TResponse> projection) => RespondAsync(operation, projection);

        public Task<IActionResult> RespondCreatedValueAsync<TValue, TResponse>(
            Task<Result<TValue>> operation,
            Func<TValue, TResponse> projection,
            Func<TValue, string> location) => RespondCreatedAsync(operation, projection, location);

        public Task<IActionResult> RespondOptionalValueAsync<TValue, TResponse>(
            Task<Result<TValue?>> operation,
            Func<TValue, TResponse> projection)
            where TValue : class => RespondOptionalAsync(operation, projection);
    }

    private sealed record DoubledResponse(int doubled);

    private sealed record IdResponse(Guid id);

    private sealed record NameResponse(string name);
}
