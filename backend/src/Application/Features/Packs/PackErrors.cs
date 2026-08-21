using Application.Errors;

namespace Application.Features.Packs;

/// <summary>Erros funcionais estáveis da gestão de packs.</summary>
public static class PackErrors
{
    public static readonly Error PackTypeNotFound = Error.Create(
        "pack_type_not_found",
        ErrorCategory.NotFound,
        "Pack type was not found."
    );

    public static readonly Error ClientSessionPackNotFound = Error.Create(
        "client_session_pack_not_found",
        ErrorCategory.NotFound,
        "Client session pack was not found."
    );

    public static readonly Error PackTypeInactive = Error.Create(
        "pack_type_inactive",
        ErrorCategory.Conflict,
        "An inactive pack type cannot be assigned."
    );

    public static readonly Error ClientSessionPackReferenced = Error.Create(
        "client_session_pack_referenced",
        ErrorCategory.Conflict,
        "A client session pack referenced by a session cannot be cancelled."
    );

    public static readonly Error ClientSessionPackUsed = Error.Create(
        "client_session_pack_used",
        ErrorCategory.Conflict,
        "A used client session pack cannot be cancelled."
    );

    public static readonly Error ExpectedEndDateBeforePurchase = Error.Validation(
    [
        new ValidationError(
            "ExpectedEndDate",
            "expected_end_date_before_purchase",
            "Expected end date cannot be before purchase date."
        )
    ]);

    public static readonly Error TrainerOnly = Error.Create(
        "packs_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage their packs."
    );

    public static Error PackIdRequired() => Error.Validation([
        new ValidationError(
            "PackId",
            "pack_id_required",
            "Pack ID is required."
        )
    ]);

    public static Error PurchaseDateInFuture() => Error.Validation([
        new ValidationError(
            "PurchaseDate",
            "purchase_date_future",
            "Purchase date cannot be in the future."
        )
    ]);
}
