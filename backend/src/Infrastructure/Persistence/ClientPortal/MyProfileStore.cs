using Application.Features.ClientPortal.Abstractions;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.ClientPortal;

/// <summary>
/// Aplica as alterações de contacto que o próprio cliente pode fazer ao seu perfil.
/// </summary>
internal sealed class MyProfileStore : IMyProfileStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _constraintTranslator;

    public MyProfileStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator constraintTranslator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _constraintTranslator = constraintTranslator
            ?? throw new ArgumentNullException(nameof(constraintTranslator));
    }

    public async Task<UpdateMyProfileOutcome> UpdateAsync(
        Guid trainerId,
        Guid clientUserId,
        UpdateMyProfileWriteModel writeModel,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writeModel);

        var client = await _dbContext.Clients
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OwnerTrainerId == trainerId &&
                    candidate.UserId == clientUserId &&
                    candidate.IsActive,
                cancellationToken);

        if (client is null)
            return UpdateMyProfileOutcome.NotFound;

        // Os cinco argumentos não editáveis vêm da entidade carregada, não do pedido.
        client.UpdateProfile(
            client.Name,
            writeModel.ContactEmail,
            writeModel.Phone,
            client.BirthDate,
            client.Sex,
            client.Objective,
            client.Notes,
            writeModel.EmergencyContactName,
            writeModel.EmergencyContactPhone,
            now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            var translated = _constraintTranslator.TryTranslate(
                exception,
                PersistenceOperation.UpdateClient,
                out var error);

            if (translated && error?.Code == "client_email_already_exists")
                return UpdateMyProfileOutcome.DuplicateEmail;
            if (translated && error?.Code == "client_phone_already_exists")
                return UpdateMyProfileOutcome.DuplicatePhone;
            throw;
        }

        return UpdateMyProfileOutcome.Updated(MyProfileMapping.ToDto(client));
    }
}
