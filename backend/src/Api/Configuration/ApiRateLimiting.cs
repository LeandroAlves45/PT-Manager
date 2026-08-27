using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Configuration;

/// <summary>Configura limites globais e politícas específicas de segurança.</summary>
public static class ApiRateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.User.Identity?.IsAuthenticated is true
                    ? FixedWindow(UserKey(context), 300, TimeSpan.FromMinutes(1))
                    : FixedWindow(IpKey(context), 60, TimeSpan.FromMinutes(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.Login,
                context => FixedWindow(IpKey(context), 10, TimeSpan.FromMinutes(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.SignUp,
                context => FixedWindow(IpKey(context), 3, TimeSpan.FromHours(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.Refresh,
                context => FixedWindow(IpKey(context), 30, TimeSpan.FromMinutes(5)));

            options.AddPolicy(ApiRateLimitPolicyNames.Logout,
                context => FixedWindow(IpKey(context), 10, TimeSpan.FromMinutes(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.CsrfBootstrap,
                context => FixedWindow(IpKey(context), 10, TimeSpan.FromMinutes(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.PasswordResetRequest,
                context => FixedWindow(IpKey(context), 3, TimeSpan.FromHours(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.PasswordResetComplete,
                context => FixedWindow(IpKey(context), 5, TimeSpan.FromHours(15)));

            options.AddPolicy(ApiRateLimitPolicyNames.EmailConfirmation,
                context => FixedWindow(IpKey(context), 10, TimeSpan.FromMinutes(15)));

            options.AddPolicy(ApiRateLimitPolicyNames.EmailConfirmationResend,
                context => FixedWindow(IpKey(context), 3, TimeSpan.FromHours(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.InviteClient,
                context => FixedWindow(UserKey(context), 10, TimeSpan.FromHours(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.GoogleSignIn,
                context => FixedWindow(IpKey(context), 10, TimeSpan.FromMinutes(1)));

            options.AddPolicy(ApiRateLimitPolicyNames.GoogleLink,
                context => FixedWindow(UserAndIpKey(context), 10, TimeSpan.FromMinutes(15)));

            options.AddPolicy(ApiRateLimitPolicyNames.ChangePassword,
                context => FixedWindow(UserAndIpKey(context), 5, TimeSpan.FromMinutes(15)));

            options.AddPolicy(ApiRateLimitPolicyNames.Moderation,
                context => FixedWindow(UserAndIpKey(context), 30, TimeSpan.FromMinutes(1)));
        });

        return services;
    }

    private static RateLimitPartition<string> FixedWindow(
        string partitionKey,
        int permitLimit,
        TimeSpan window
    ) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });

    private static string UserKey(HttpContext context)
    {
        var subject = context.User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? IpKey(context)
            : $"user:{subject}";
    }

    private static string IpKey(HttpContext context) =>
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static string UserAndIpKey(HttpContext context) =>
        $"{UserKey(context)}:{IpKey(context)}";

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests",
            Detail = "The request limit has exceeded. Retry after the indicated interval.",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["correlation_id"] = context.HttpContext.TraceIdentifier;

        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
