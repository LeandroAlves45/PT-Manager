using System.Text.Json;
using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.Notifications.EnqueueNotification;

/// <summary>Valida o contrato antes de construir os value objects do Domain.</summary>
public sealed class EnqueueNotificationCommandValidator : AbstractValidator<EnqueueNotificationCommand>
{
    public EnqueueNotificationCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(command => command.ClientId)
            .Must(value => !value.HasValue || value.Value != Guid.Empty)
            .WithErrorCode("notification_client_id_invalid");

        RuleFor(command => command.RecipientEmail)
            .NotEmpty()
            .WithErrorCode("notification_recipient_required")
            .MaximumLength(255)
            .WithErrorCode("notification_recipient_invalid")
            .EmailAddress()
            .WithErrorCode("notification_recipient_invalid");

        RuleFor(command => command.NotificationType)
            .NotEmpty()
            .WithErrorCode("notification_type_required")
            .MaximumLength(50)
            .WithErrorCode("notification_type_too_long");

        RuleFor(command => command.TemplateKey)
            .NotEmpty()
            .WithErrorCode("notification_template_required")
            .MaximumLength(255)
            .WithErrorCode("notification_template_too_long");

        RuleFor(command => command.OperationKey)
            .NotEmpty()
            .WithErrorCode("notification_operation_key_required")
            .MaximumLength(100)
            .WithErrorCode("notification_operation_key_too_long")
            .Matches("^[A-Za-z0-9._:-]+$")
            .WithErrorCode("notification_operation_key_invalid");

        RuleFor(command => command.CorrelationId)
            .NotEmpty()
            .WithErrorCode("notification_correlation_id_required");

        RuleFor(command => command.ScheduledAt)
            .Must(value => !value.HasValue || value.Value.UtcDateTime >= clock.UtcNow)
            .WithErrorCode("notification_scheduled_in_past");

        RuleFor(command => command.TemplateDataJson)
            .Must(BeSafeJsonObject)
            .WithErrorCode("notification_template_data_invalid")
            .WithMessage("Template data must be a valid JSON object.")
            .When(command => !string.IsNullOrWhiteSpace(command.TemplateDataJson));
    }

    private static bool BeSafeJsonObject(string? value)
    {
        try
        {
            using var document = JsonDocument.Parse(value!);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                !ContainsSensitiveProperty(document.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool ContainsSensitiveProperty(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (IsSensitiveName(property.Name) || ContainsSensitiveProperty(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsSensitiveProperty(item))
                    return true;
            }
        }

        return false;
    }

    private static bool IsSensitiveName(string name)
    {
        // Separadores e casing são controlados pelo input e não podem contornar a denylist.
        var normalizedName = new string(name
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

        return normalizedName.Contains("token", StringComparison.Ordinal) ||
            normalizedName.Contains("password", StringComparison.Ordinal) ||
            normalizedName.Contains("secret", StringComparison.Ordinal) ||
            normalizedName.Contains("cookie", StringComparison.Ordinal) ||
            normalizedName.Contains("authorization", StringComparison.Ordinal) ||
            normalizedName.Contains("apikey", StringComparison.Ordinal);
    }
}
