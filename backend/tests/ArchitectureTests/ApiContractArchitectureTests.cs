using System.Reflection;
using Api.Contracts.Common;
using Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ArchitectureTests;

/// <summary>
/// Garante que o contrato OpenAPI descreve todas as respostas da API (QG6C-CONTRATO-001).
/// </summary>
/// <remarks>
/// <para>
/// O frontend gera os seus tipos a partir do OpenAPI. Uma ação sem atributo de sucesso
/// aparece como <c>content?: never</c> e obriga a casts manuais; um erro declarado com
/// <see cref="ProblemDetails"/> simples esconde <c>correlation_id</c> e <c>errors</c>.
/// </para>
/// <para>
/// A reflexão usa <c>Public | Instance</c> sem <c>DeclaredOnly</c>: as cinco ações de
/// <see cref="ManagedExerciseVideoControllerBase"/> contam uma vez por controller concreto,
/// como acontece no routing real. Cada operação é o par controller concreto e método.
/// </para>
/// </remarks>
public sealed class ApiContractArchitectureTests
{
    private const int ExpectedConcreteControllers = 31;
    private const int ExpectedDeclaredActions = 165;
    private const int ExpectedEffectiveOperations = 170;

    private static readonly IReadOnlyList<Operation> Operations = DiscoverOperations();

    [Fact]
    public void Surface_HasExpectedControllersAndOperations()
    {
        Assert.Equal(
            ExpectedConcreteControllers,
            Operations.Select(operation => operation.Controller).Distinct().Count());

        Assert.Equal(
            ExpectedEffectiveOperations,
            Operations.Select(operation => operation.Id).Distinct().Count());

        // O mesmo método herdado conta uma única vez no código fonte.
        Assert.Equal(
            ExpectedDeclaredActions,
            Operations.Select(operation => operation.Method.MethodHandle).Distinct().Count());
    }

    [Fact]
    public void EveryOperation_DeclaresExactlyOneAttributePerSuccessStatus()
    {
        var violations = new List<string>();

        foreach (var operation in Operations)
        {
            var success = operation.MethodResponses.Where(IsSuccess).ToArray();

            if (success.Length == 0)
                violations.Add($"{operation.Id}: no 2xx response declared");

            violations.AddRange(success
                .GroupBy(response => response.StatusCode)
                .Where(group => group.Count() > 1)
                .Select(group => $"{operation.Id}: {group.Key} declared {group.Count()} times"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void SuccessResponses_AreTypedExceptNoContent()
    {
        var violations = new List<string>();

        foreach (var operation in Operations)
        {
            foreach (var response in operation.MethodResponses.Where(IsSuccess))
            {
                var hasBody = response.Type is not null && response.Type != typeof(void);

                if (response.StatusCode == StatusCodes.Status204NoContent && hasBody)
                    violations.Add($"{operation.Id}: 204 declares body {response.Type!.Name}");

                if (response.StatusCode != StatusCodes.Status204NoContent && !hasBody)
                    violations.Add($"{operation.Id}: {response.StatusCode} has no body type");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void ErrorResponses_UseApiProblemDetails()
    {
        var violations = new List<string>();

        foreach (var operation in Operations)
        {
            var errors = operation.AllResponses
                .Where(response => response.StatusCode >= StatusCodes.Status400BadRequest)
                .ToArray();

            if (errors.Length == 0)
                violations.Add($"{operation.Id}: no error response declared");

            violations.AddRange(errors
                .Where(response => response.Type != typeof(ApiProblemDetails))
                .Select(response =>
                    $"{operation.Id}: {response.StatusCode} uses {response.Type?.Name ?? "no type"}"));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void BusinessControllers_InheritTheNineMappedErrorStatuses()
    {
        // Os estados que ApiResultMapper e o pipeline podem produzir.
        int[] expected =
        [
            StatusCodes.Status400BadRequest,
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status402PaymentRequired,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status404NotFound,
            StatusCodes.Status409Conflict,
            StatusCodes.Status429TooManyRequests,
            StatusCodes.Status500InternalServerError,
            StatusCodes.Status503ServiceUnavailable
        ];

        var declared = typeof(ApiControllerBase)
            .GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: false)
            .Where(response => response.Type == typeof(ApiProblemDetails))
            .Select(response => response.StatusCode)
            .Order()
            .ToArray();

        Assert.Equal(expected, declared);
    }

    [Fact]
    public void AcceptedResponse_IsDeclaredOnlyByGoogleSignIn()
    {
        var accepted = Operations
            .Where(operation => operation.MethodResponses.Any(response =>
                response.StatusCode == StatusCodes.Status202Accepted))
            .Select(operation => operation.Id)
            .ToArray();

        Assert.Equal(["GoogleAuthController.SignInAsync"], accepted);
    }

    private static bool IsSuccess(ProducesResponseTypeAttribute response) =>
        response.StatusCode is >= 200 and < 300;

    private static IReadOnlyList<Operation> DiscoverOperations()
    {
        var controllers = typeof(ApiControllerBase).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && typeof(ControllerBase).IsAssignableFrom(type));

        return controllers
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                .Select(method => new Operation(controller, method)))
            .OrderBy(operation => operation.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record Operation(Type Controller, MethodInfo Method)
    {
        public string Id => $"{Controller.Name}.{Method.Name}";

        /// <summary>Atributos escritos no método (sucesso vive sempre aqui).</summary>
        public IReadOnlyList<ProducesResponseTypeAttribute> MethodResponses =>
            Method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true).ToArray();

        /// <summary>Método mais controller e bases, como o gerador OpenAPI os vê.</summary>
        public IReadOnlyList<ProducesResponseTypeAttribute> AllResponses =>
            MethodResponses
                .Concat(Controller.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true))
                .ToArray();
    }
}
