using Application.Features.Packs.ClientSessionPacks.AssignClientSessionPack;
using Application.Features.Packs.ClientSessionPacks.ListClientSessionPacks;
using Application.Features.Packs.ClientSessionPacks.UpdateClientSessionPackExpectedEndDate;
using Application.Features.Packs.PackTypes.CreatePackType;
using Application.Features.Packs.PackTypes.ListPackTypes;
using Application.Features.Packs.PackTypes.UpdatePackType;

namespace Application.UnitTests.Features.Packs;

public sealed class PackValidatorsTests
{
    [Fact]
    public void CreatePackType_InvalidShape_ReturnsStableCodesWithoutDuplicates()
    {
        var command = new CreatePackTypeCommand(
            string.Empty,
            0,
            -1,
            string.Empty,
            0
        );

        var result = new CreatePackTypeCommandValidator().Validate(command);

        Assert.Equal(
            [
                "pack_type_name_required",
                "pack_type_session_count_must_be_positive",
                "pack_type_price_non_negative",
                "pack_type_currency_required",
                "pack_type_expected_duration_must_be_positive"
            ],
            result.Errors.Select(error => error.ErrorCode)
        );
    }

    [Fact]
    public void CreatePackType_InvalidCurrency_ReturnsSingleFormatCode()
    {
        var command = new CreatePackTypeCommand("Pack", 10, 10000, "EU1", null);

        var result = new CreatePackTypeCommandValidator().Validate(command);

        Assert.Collection(
            result.Errors,
            error => Assert.Equal("pack_type_currency_invalid", error.ErrorCode)
        );
    }

    [Fact]
    public void UpdatePackType_InvalidShape_ReusesCreateCodesAndAddsRequiredId()
    {
        var command = new UpdatePackTypeCommand(
            Guid.Empty,
            string.Empty,
            0,
            -1,
            string.Empty,
            0
        );

        var result = new UpdatePackTypeCommandValidator().Validate(command);

        Assert.Equal(
            [
                "pack_type_id_required",
                "pack_type_name_required",
                "pack_type_session_count_must_be_positive",
                "pack_type_price_non_negative",
                "pack_type_currency_required",
                "pack_type_expected_duration_must_be_positive"
            ],
            result.Errors.Select(error => error.ErrorCode)
        );
    }

    [Fact]
    public void ListPackTypes_InvalidFilters_ReturnsStableCodes()
    {
        var query = new ListPackTypesQuery(
            new string('x', 256),
            (PackTypeActivityFilter)999,
            0,
            101
        );

        var result = new ListPackTypesQueryValidator().Validate(query);

        Assert.Equal(
            [
                "pack_type_search_too_long",
                "pack_type_activity_invalid",
                "page_number_invalid",
                "page_size_invalid"
            ],
            result.Errors.Select(error => error.ErrorCode)
        );
    }

    [Fact]
    public void AssignClientSessionPack_InvalidShape_ReturnsStableCodes()
    {
        var command = new AssignClientSessionPackCommand(
            Guid.Empty,
            Guid.Empty,
            default,
            null
        );

        var result = new AssignClientSessionPackCommandValidator().Validate(command);

        Assert.Equal(
            [
                "client_session_pack_client_id_required",
                "pack_type_id_required",
                "client_session_pack_purchase_date_required"
            ],
            result.Errors.Select(error => error.ErrorCode)
        );
    }

    [Fact]
    public void AssignClientSessionPack_EndBeforePurchase_ReturnsStableCode()
    {
        var command = new AssignClientSessionPackCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 14),
            new DateOnly(2026, 8, 13)
        );

        var result = new AssignClientSessionPackCommandValidator().Validate(command);

        Assert.Collection(
            result.Errors,
            error => Assert.Equal(
                "expected_end_date_before_purchase",
                error.ErrorCode
            )
        );
    }

    [Fact]
    public void ListClientSessionPacks_InvalidFilters_ReturnsStableCodes()
    {
        var query = new ListClientSessionPacksQuery(
            Guid.Empty,
            (ClientSessionPackActivityFilter)999,
            0,
            101
        );

        var result = new ListClientSessionPacksQueryValidator().Validate(query);

        Assert.Equal(
            [
                "client_session_pack_client_id_invalid",
                "client_session_pack_activity_invalid",
                "page_number_invalid",
                "page_size_invalid"
            ],
            result.Errors.Select(error => error.ErrorCode)
        );
    }

    [Fact]
    public void UpdateExpectedEndDate_EmptyId_ReturnsStableCode()
    {
        var command = new UpdateClientSessionPackExpectedEndDateCommand(
            Guid.Empty,
            null
        );

        var result = new UpdateClientSessionPackExpectedEndDateCommandValidator()
            .Validate(command);

        Assert.Collection(
            result.Errors,
            error => Assert.Equal("client_session_pack_id_required", error.ErrorCode)
        );
    }
}
